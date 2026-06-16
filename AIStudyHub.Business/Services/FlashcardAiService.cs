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
        var maxBatchRetries = 5;
        FlashcardResponseAiDto[]? parsed = null;

        for (int attempt = 0; attempt < maxBatchRetries && parsed is null; attempt++)
        {
            var existingCards = flashcards.Count == 0
                ? "None"
                : string.Join(
                    "\n",
                    flashcards.Select(x => $"- {x.Front}"));

            var prompt = $$"""
You are a JSON API.

Return ONLY valid JSON. No markdown, no explanations, no code fences.

Generate EXACTLY {{request.NumberOfFlashcards}} flashcards as a JSON array.

Use ONLY information from the CONTEXT.

Do not generate a flashcard similar to:

{{existingCards}}

Response format (array of exactly {{request.NumberOfFlashcards}} objects):

[
  { "front": "string", "back": "string" },
  { "front": "string", "back": "string" }
]

Rules:
- Return exactly {{request.NumberOfFlashcards}} objects in the array.
- Each "front" and "back" must be a non-empty string.
- All "front" values must be unique.
- Stay strictly within the CONTEXT below.

CONTEXT:
{{context}}
""";

            try
            {
                var aiText = await _localAIService.SendMessageAsync(prompt);
                _logger.LogDebug("AI raw response length={Length}", aiText.Length);
                _logger.LogTrace("AI raw response: {Raw}", aiText);

                var cleaned = ExtractFirstJsonArray(aiText);
                _logger.LogDebug("AI cleaned payload length={Length}", cleaned.Length);
                _logger.LogTrace("AI cleaned payload: {Cleaned}", cleaned);

                using var docJson = JsonDocument.Parse(cleaned);
                var root = docJson.RootElement;

                if (root.ValueKind != JsonValueKind.Array)
                {
                    _logger.LogWarning(
                        "AI response is not a JSON array (attempt {Attempt})",
                        attempt + 1);
                    continue;
                }

                var batch = new List<FlashcardResponseAiDto>();
                var seenFronts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var skipped = 0;

                foreach (var element in root.EnumerateArray())
                {
                    if (element.ValueKind != JsonValueKind.Object)
                    {
                        skipped++;
                        continue;
                    }

                    if (!element.TryGetProperty("front", out var frontProp) ||
                        !element.TryGetProperty("back", out var backProp))
                    {
                        skipped++;
                        continue;
                    }

                    var front = frontProp.ValueKind == JsonValueKind.String
                        ? frontProp.GetString()?.Trim()
                        : null;
                    var back = backProp.ValueKind == JsonValueKind.String
                        ? backProp.GetString()?.Trim()
                        : null;

                    if (string.IsNullOrWhiteSpace(front) ||
                        string.IsNullOrWhiteSpace(back))
                    {
                        skipped++;
                        continue;
                    }

                    front = Regex.Replace(front, @"\s*\[[^\]]+\]", string.Empty).Trim();
                    back = Regex.Replace(back, @"\s*\[[^\]]+\]", string.Empty).Trim();

                    if (string.IsNullOrWhiteSpace(front) ||
                        !seenFronts.Add(front))
                    {
                        skipped++;
                        continue;
                    }

                    batch.Add(new FlashcardResponseAiDto(front, back));

                    if (batch.Count >= request.NumberOfFlashcards)
                        break;
                }

                _logger.LogDebug(
                    "AI batch: parsed={Got} skipped={Skipped} requested={Requested}",
                    batch.Count, skipped, request.NumberOfFlashcards);

                if (batch.Count == request.NumberOfFlashcards)
                {
                    parsed = batch.ToArray();
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to parse AI JSON response (attempt {Attempt})",
                    attempt + 1);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "AI call failed (attempt {Attempt})",
                    attempt + 1);
            }

        }

        if (parsed is not null)
        {
            flashcards.AddRange(parsed);
        }
        else
        {
            _logger.LogError(
                "Could not generate a valid batch of {Requested} flashcards after {Max} attempts",
                request.NumberOfFlashcards, maxBatchRetries);
        }

        if (flashcards.Count < request.NumberOfFlashcards)
        {
            _logger.LogWarning(
                "Only generated {Got}/{Requested} flashcards",
                flashcards.Count, request.NumberOfFlashcards);
        }

        return new FlashcardsAiResponseDto(flashcards);
    }

    private static string ExtractFirstJsonArray(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        text = Regex.Replace(text, @"[\u0000-\u0008\u000B\u000C\u000E-\u001F]", string.Empty);
        text = text.Trim();

        var fenced = Regex.Match(
            text,
            @"```(?:json)?\s*(\[[\s\S]*?\])\s*```",
            RegexOptions.IgnoreCase);

        var candidate = fenced.Success ? fenced.Groups[1].Value : FindBalancedArray(text);

        if (string.IsNullOrEmpty(candidate))
            return text;

        var lastClose = candidate.LastIndexOf(']');
        if (lastClose >= 0)
            candidate = candidate[..(lastClose + 1)];

        return candidate;
    }

    private static string FindBalancedArray(string text)
    {
        var start = text.IndexOf('[');
        if (start < 0)
            return string.Empty;

        var depth = 0;
        var inString = false;
        var escape = false;

        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];

            if (escape)
            {
                escape = false;
                continue;
            }

            if (inString)
            {
                if (c == '\\') escape = true;
                else if (c == '"') inString = false;
                continue;
            }

            if (c == '"') inString = true;
            else if (c == '[') depth++;
            else if (c == ']')
            {
                depth--;
                if (depth == 0)
                    return text.Substring(start, i - start + 1);
            }
        }

        return string.Empty;
    }

}

