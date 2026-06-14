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
    private readonly ILocalAIService _localAIService;
    private readonly RagOptions _options;
    private readonly ILogger<FlashcardAiService> _logger;

    public FlashcardAiService(IUnitOfWork unitOfWork, ILocalAIService openAiService, IOptions<RagOptions> options, ILogger<FlashcardAiService> logger)
    {
        _unitOfWork = unitOfWork;
        _localAIService = openAiService;
        _options = options.Value;
        _logger = logger;
    }
    public async Task<FlashcardsAiResponseDto> GenerateFlashcardsAsync(
        Guid documentId,
        CreateFlashcardsViaAiRequestDto request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (request.NumberOfFlashcards <= 0 || request.NumberOfFlashcards > 20)
            throw new ArgumentOutOfRangeException(
                nameof(request.NumberOfFlashcards),
                "Number of flashcards must be between 1 and 20.");

        var document = await _unitOfWork.Documents.GetByIdAsync(
            documentId,
            cancellationToken);

        if (document is null)
            throw new KeyNotFoundException("Document not found");

        var chunks = await _unitOfWork.DocumentChunks
            .Query()
            .Where(c => c.DocumentId == documentId)
            .OrderBy(c => c.OrderIndex)
            .ToListAsync(cancellationToken);

        var context = string.Join(
            "\n\n",
            chunks.Select(c => c.ChunkJson ?? ""));
        Console.Write("Context: "+context);
        var flashcards = new List<FlashcardResponseAiDto>();

        for (int i = 0; i < request.NumberOfFlashcards; i++)
        {
            var existingCards = flashcards.Count == 0
                ? "None"
                : string.Join(
                    "\n",
                    flashcards.Select(x => $"- {x.Front}"));

            var prompt = $"""
You are a JSON API.

Return ONLY valid JSON.

Do not return:
- markdown
- explanations
- comments
- code fences

Generate EXACTLY ONE flashcard.

Use ONLY information from the CONTEXT.

Do not generate a flashcard similar to:

{existingCards}

Response format:

{" { \"front\": \"string\",\r\n  \"back\": \"string\"    }"}
            



CONTEXT:
{context}
""";

            var aiText = await _localAIService.SendMessageAsync(prompt);

            try
            {
                using var docJson = JsonDocument.Parse(aiText);

                var front = docJson.RootElement
                    .GetProperty("front")
                    .GetString()?
                    .Trim();

                var back = docJson.RootElement
                    .GetProperty("back")
                    .GetString()?
                    .Trim();

                if (string.IsNullOrWhiteSpace(front) ||
                    string.IsNullOrWhiteSpace(back))
                {
                    continue;
                }

                front = Regex.Replace(
                    front,
                    @"\s*\[[^\]]+\]",
                    string.Empty).Trim();

                back = Regex.Replace(
                    back,
                    @"\s*\[[^\]]+\]",
                    string.Empty).Trim();

                if (flashcards.Any(x =>
                    x.Front.Equals(front,
                    StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                flashcards.Add(
                    new FlashcardResponseAiDto(front, back));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to generate flashcard {Index}",
                    i + 1);
            }
        }

        return new FlashcardsAiResponseDto(flashcards);
    }

}

