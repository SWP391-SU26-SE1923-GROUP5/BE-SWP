using AIStudyHub.Business.DTOs.Flashcards;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Options;
using AIStudyHub.Data.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace AIStudyHub.Business.Services;

public sealed class FlashcardAiService : IFlashcardAiService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOpenAIService _openAiService;
    private readonly RagOptions _options;
    private readonly ILogger<FlashcardAiService> _logger;

    public FlashcardAiService(IUnitOfWork unitOfWork, IOpenAIService openAiService, IOptions<RagOptions> options, ILogger<FlashcardAiService> logger)
    {
        _unitOfWork = unitOfWork;
        _openAiService = openAiService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<FlashcardsAiResponseDto> GenerateFlashcardsAsync(Guid documentId, CreateFlashcardsViaAiRequestDto request, Guid userId, CancellationToken cancellationToken = default)
    {
        if (request.NumberOfFlashcards <= 0 || request.NumberOfFlashcards > 20)
            throw new ArgumentOutOfRangeException(nameof(request.NumberOfFlashcards), "Number of flashcards must be between 1 and 20.");

        var document = await _unitOfWork.Documents.GetByIdAsync(documentId, cancellationToken);
        if (document is null) throw new KeyNotFoundException("Document not found");

        // Build context from chunks
        var chunks = await _unitOfWork.DocumentChunks.Query().Where(c => c.DocumentId == documentId).OrderBy(c => c.OrderIndex).ToListAsync(cancellationToken);
        var context = string.Join("\n\n", chunks.Select(c => c.ChunkJson ?? ""));

        var instruction = "You are to generate flashcards from the provided context. Return ONLY valid JSON with property 'flashcards' which is an array of objects {\"front\":string, \"back\":string}. Do not include explanations or extra text.";

        var prompt = $"{instruction}\n\nCONTEXT:\n{context}\n\nGenerate {request.NumberOfFlashcards} flashcards in JSON format.";

        var aiText = await _openAiService.SendMessageAsync(prompt);

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var doc = JsonDocument.Parse(aiText);
            if (doc.RootElement.TryGetProperty("flashcards", out var flashcardsEl))
            {
                var list = new List<FlashcardResponseAiDto>();
                foreach (var item in flashcardsEl.EnumerateArray())
                {
                    var front = item.GetProperty("front").GetString() ?? string.Empty;
                    var back = item.GetProperty("back").GetString() ?? string.Empty;

                    front = Regex.Replace(front, @"\s*\[[^\]]+\]", string.Empty).Trim();
                    back = Regex.Replace(back, @"\s*\[[^\]]+\]", string.Empty).Trim();

                    list.Add(new FlashcardResponseAiDto(front, back));
                }

                return new FlashcardsAiResponseDto(list);
            }

            throw new JsonException("AI returned JSON but missing 'flashcards' property");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse AI response for flashcards");
            throw new InvalidOperationException("AI did not return valid flashcard JSON.");
        }
    }
}
