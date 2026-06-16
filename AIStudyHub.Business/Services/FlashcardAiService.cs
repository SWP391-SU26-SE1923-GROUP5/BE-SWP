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
        Console.Write("Context: " + context);
        var flashcards = new List<FlashcardResponseAiDto>();
        var seenFronts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var maxRetriesPerCard = 3;
        var maxConsecutiveFailures = 5;
        var consecutiveFailures = 0;

        for (int i = 0; i < request.NumberOfFlashcards; i++)
        {
            var card = await TryGenerateSingleFlashcardAsync(
                context,
                flashcards,
                i + 1,
                request.NumberOfFlashcards,
                seenFronts,
                maxRetriesPerCard,
                cancellationToken);

            if (card is null)
            {
                consecutiveFailures++;
                if (consecutiveFailures >= maxConsecutiveFailures)
                {
                    _logger.LogError(
                        "Aborting flashcard generation after {Consecutive} consecutive failures",
                        consecutiveFailures);
                    break;
                }
                continue;
            }

            consecutiveFailures = 0;
            flashcards.Add(card);
        }

        if (flashcards.Count < request.NumberOfFlashcards)
        {
            _logger.LogWarning(
                "Only generated {Got}/{Requested} flashcards",
                flashcards.Count, request.NumberOfFlashcards);
        }

        return new FlashcardsAiResponseDto(flashcards);
    }

    private async Task<FlashcardResponseAiDto?> TryGenerateSingleFlashcardAsync(
        string context,
        List<FlashcardResponseAiDto> alreadyGenerated,
        int currentIndex,
        int totalRequested,
        HashSet<string> seenFronts,
        int maxRetries,
        CancellationToken cancellationToken)
    {
        var existingCards = alreadyGenerated.Count == 0
            ? "None"
            : string.Join("\n", alreadyGenerated.Select(x => $"- {x.Front}"));

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            var prompt = $$"""
You are a JSON API.

Return ONLY valid JSON. No markdown, no explanations, no code fences.

Generate EXACTLY ONE flashcard as a single JSON object (not an array).

Use ONLY information from the CONTEXT.

Do not generate a flashcard similar to:

|{{existingCards}}

Response format (a single object):

{ "front": "string", "back": "string" }

Rules:
- Return exactly one object (NOT an array, NOT a list).
- "front" and "back" must be non-empty strings.
- "front" must be unique compared to the existing cards listed above.
- "front" should be a clear question, term, or prompt.
- "back" should be a concise answer or explanation (1-3 sentences).
- Stay strictly within the CONTEXT below.
- Do not repeat a "front" value from the existing cards list.

This is card {{currentIndex}} of {{totalRequested}}.

CONTEXT:
{{context}}
""";

            try
            {
                var aiText = await _localAIService.SendMessageAsync(prompt);
                _logger.LogDebug(
                    "AI raw response length={Length} (card {Index}/{Total}, attempt {Attempt})",
                    aiText.Length, currentIndex, totalRequested, attempt + 1);
                _logger.LogTrace("AI raw response: {Raw}", aiText);

                var cleaned = ExtractFirstJsonObject(aiText);
                _logger.LogDebug(
                    "AI cleaned payload length={Length} (card {Index}/{Total}, attempt {Attempt})",
                    cleaned.Length, currentIndex, totalRequested, attempt + 1);
                _logger.LogTrace("AI cleaned payload: {Cleaned}", cleaned);

                using var docJson = JsonDocument.Parse(cleaned);
                var root = docJson.RootElement;
                _logger.LogWarning(docJson.RootElement.ToString());
                JsonElement obj;
                if (root.ValueKind == JsonValueKind.Array)
                {
                    if (root.GetArrayLength() == 0)
                    {
                        _logger.LogWarning(
                            "AI returned empty array (card {Index}, attempt {Attempt})",
                            currentIndex, attempt + 1);
                        continue;
                    }
                    obj = root[0];
                }
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    obj = root;
                }
                else
                {
                    _logger.LogWarning(
                        "AI response is not a JSON object (card {Index}, attempt {Attempt})",
                        currentIndex, attempt + 1);
                    continue;
                }

                if (!obj.TryGetProperty("front", out var frontProp) ||
                    !obj.TryGetProperty("back", out var backProp))
                {
                    _logger.LogWarning(
                        "AI response missing front/back (card {Index}, attempt {Attempt})",
                        currentIndex, attempt + 1);
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
                    _logger.LogWarning(
                        "AI returned empty front/back (card {Index}, attempt {Attempt})",
                        currentIndex, attempt + 1);
                    continue;
                }

                front = Regex.Replace(front, @"\s*\[[^\]]+\]", string.Empty).Trim();
                back = Regex.Replace(back, @"\s*\[[^\]]+\]", string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(front))
                {
                    _logger.LogWarning(
                        "AI front became empty after cleanup (card {Index}, attempt {Attempt})",
                        currentIndex, attempt + 1);
                    continue;
                }

                if (!seenFronts.Add(front))
                {
                    _logger.LogWarning(
                        "AI returned duplicate front '{Front}' (card {Index}, attempt {Attempt})",
                        front, currentIndex, attempt + 1);
                    continue;
                }

                _logger.LogDebug(
                    "Generated flashcard {Index}/{Total}: {Front}",
                    currentIndex, totalRequested, front);
                return new FlashcardResponseAiDto(front, back);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to parse AI JSON response (card {Index}, attempt {Attempt})",
                    currentIndex, attempt + 1);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "AI call failed (card {Index}, attempt {Attempt})",
                    currentIndex, attempt + 1);
            }
        }

        _logger.LogError(
            "Could not generate flashcard {Index}/{Total} after {Max} attempts",
            currentIndex, totalRequested, maxRetries);
        return null;
    }

    private static string ExtractFirstJsonObject(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        text = Regex.Replace(text, @"[\u0000-\u0008\u000B\u000C\u000E-\u001F]", string.Empty);
        text = text.Trim();

        var fenced = Regex.Match(
            text,
            @"```(?:json)?\s*(\{[\s\S]*?\})\s*```",
            RegexOptions.IgnoreCase);

        var candidate = fenced.Success ? fenced.Groups[1].Value : FindBalancedObject(text);

        if (string.IsNullOrEmpty(candidate))
            return text;

        var lastClose = candidate.LastIndexOf('}');
        if (lastClose >= 0)
            candidate = candidate[..(lastClose + 1)];

        return candidate;
    }

    private static string FindBalancedObject(string text)
    {
        var start = text.IndexOf('{');
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
            else if (c == '{') depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                    return text.Substring(start, i - start + 1);
            }
        }

        return string.Empty;
    }

}
