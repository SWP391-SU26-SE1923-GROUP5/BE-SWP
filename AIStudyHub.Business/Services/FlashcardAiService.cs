using AIStudyHub.Business.DTOs.Flashcards;
using AIStudyHub.Business.DTOs.Rag;
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
        if (request.NumberOfFlashcards <= 0 || request.NumberOfFlashcards > 7)
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
            chunks.Select(c => ExtractChunkContent(c.ChunkJson)));
        _logger.LogDebug("Flashcard context length={Length} for document {DocumentId}", context.Length, documentId);

        var persistedCards = await _unitOfWork.Flashcards
            .Query()
            .Where(f => f.DocumentId == documentId)
            .OrderBy(f => f.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        _logger.LogDebug(
            "Loaded {Count} persisted flashcards for document {DocumentId} into history",
            persistedCards.Count, documentId);

        var flashcards = new List<FlashcardResponseAiDto>(
            persistedCards.Select(f => new FlashcardResponseAiDto(f.Front, f.Back)));
        var seenFronts = new HashSet<string>(
            persistedCards.Select(f => f.Front),
            StringComparer.OrdinalIgnoreCase);
        var seenBacks = new HashSet<string>(
            persistedCards.Select(f => f.Back),
            StringComparer.OrdinalIgnoreCase);
        var maxRetriesPerCard = 3;
        var maxConsecutiveFailures = 5;
        var consecutiveFailures = 0;

        for (int i = 0; i < request.NumberOfFlashcards; i++)
        {
            var recentRejections = new List<string>(maxRetriesPerCard);
            var card = await TryGenerateSingleFlashcardAsync(
                context,
                flashcards,
                i + 1 + persistedCards.Count,
                request.NumberOfFlashcards + persistedCards.Count,
                seenFronts,
                seenBacks,
                recentRejections,
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

            if (card.Front.StartsWith("__SKIP__:", StringComparison.Ordinal))
            {
                _logger.LogInformation(
                    "Stopping flashcard generation at card {Index}: AI reported no more distinct topics in CONTEXT",
                    i + 1);
                break;
            }

            consecutiveFailures = 0;
            flashcards.Add(card);
        }

        var newlyGenerated = flashcards.Count - persistedCards.Count;
        if (newlyGenerated < request.NumberOfFlashcards)
        {
            _logger.LogWarning(
                "Only generated {Got}/{Requested} NEW flashcards ({Total} total persisted+new for document)",
                newlyGenerated, request.NumberOfFlashcards, flashcards.Count);
        }

        return new FlashcardsAiResponseDto(flashcards);
    }

    private async Task<FlashcardResponseAiDto?> TryGenerateSingleFlashcardAsync(
        string context,
        List<FlashcardResponseAiDto> alreadyGenerated,
        int currentIndex,
        int totalRequested,
        HashSet<string> seenFronts,
        HashSet<string> seenBacks,
        List<string> recentRejections,
        int maxRetries,
        CancellationToken cancellationToken)
    {
        const int existingCardsLimit = 8;
        var tailCards = alreadyGenerated
            .TakeLast(existingCardsLimit)
            .Select(x => $"- front: \"{x.Front}\"  back: \"{x.Back}\"");
        var existingCards = alreadyGenerated.Count == 0
            ? "None"
            : (alreadyGenerated.Count > existingCardsLimit
                ? $"[showing last {existingCardsLimit} of {alreadyGenerated.Count}]\n" + string.Join("\n", tailCards)
                : string.Join("\n", tailCards));

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            string previousRejection;
                if (recentRejections.Count == 0)
                {
                    previousRejection = string.Empty;
                }
                else
                {
                    var rejectedList = string.Join("\n", recentRejections.Select(r => $"- \"{r}\""));
                    previousRejection =
                        "\nATTENTION — your previous attempts for this card were rejected. " +
                        "You must NOT produce any of these fronts (or any close paraphrase of them):\n\n"
                        + rejectedList
                        + "\n\nPick a front about a clearly different concept. " +
                        "If the CONTEXT does not contain enough distinct concepts, " +
                        "return an object with both \"front\" and \"back\" set to the literal string \"SKIP\".\n\n";
                }

            var prompt = $$"""
You are a JSON API.

Return ONLY valid JSON. No markdown, no explanations, no code fences.

Generate EXACTLY ONE flashcard as a single JSON object (not an array).

Use ONLY information from the CONTEXT.

Already generated cards (do NOT repeat or paraphrase any front OR back):

{{existingCards}}
{{previousRejection}}
Response format (a single object):

{ "front": "string", "back": "string" }

Rules:
- Return exactly one object (NOT an array, NOT a list). To skip this card (no more distinct topics left), return an object with both "front" and "back" set to the literal string "SKIP".
- "front" and "back" must be non-empty strings.
- "front" must be lexically unique against every "front" above.
- "back" must be lexically unique against every "back" above.
- "front" must cover a clearly different concept than every front above. Do not paraphrase, reword, or reformulate any existing front.
- "front" should be a clear question, term, or prompt.
- "back" should be a concise answer or explanation (1-3 sentences).
- Stay strictly within the CONTEXT below.
- Do not repeat, paraphrase, or overlap any existing card's front or back.

This is card {{currentIndex}} of {{totalRequested}}.

CONTEXT:
{{context}}
""";


            try
            {
                var chatHistory = BuildChatHistory(alreadyGenerated);
                var aiText = await _localAIService.SendChatAsync(
                    systemPrompt: prompt,
                    history: chatHistory,
                    userMessage: BuildUserTurn(currentIndex, totalRequested),
                    cancellationToken: cancellationToken);
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

                if ((string.IsNullOrWhiteSpace(front) || string.IsNullOrWhiteSpace(back)) &&
                    !(string.IsNullOrEmpty(front) && string.IsNullOrEmpty(back)))
                {
                    _logger.LogWarning(
                        "AI returned empty front/back (card {Index}, attempt {Attempt})",
                        currentIndex, attempt + 1);
                    continue;
                }

                if (string.IsNullOrEmpty(front) && string.IsNullOrEmpty(back))
                {
                    _logger.LogInformation(
                        "AI signalled skip for card {Index}: no more distinct topics in CONTEXT",
                        currentIndex);
                    return new FlashcardResponseAiDto(
                        $"__SKIP__:{currentIndex}",
                        $"__SKIP__:{currentIndex}");
                }

                if (front == "SKIP" && back == "SKIP")
                {
                    _logger.LogInformation(
                        "AI signalled skip for card {Index}: no more distinct topics in CONTEXT",
                        currentIndex);
                    return new FlashcardResponseAiDto(
                        $"__SKIP__:{currentIndex}",
                        $"__SKIP__:{currentIndex}");
                }

                front = Regex.Replace(front!, @"\s*\[[^\]]+\]", string.Empty).Trim();
                back = Regex.Replace(back!, @"\s*\[[^\]]+\]", string.Empty).Trim();

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
                    if (!recentRejections.Contains(front))
                        recentRejections.Add(front);
                    continue;
                }

                if (!seenBacks.Add(back))
                {
                    _logger.LogWarning(
                        "AI returned duplicate back '{Back}' (card {Index}, attempt {Attempt})",
                        back, currentIndex, attempt + 1);
                    seenFronts.Remove(front);
                    if (!recentRejections.Contains(front))
                        recentRejections.Add(front);
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

    private const int ChatHistoryLimit = 16;

    private static IReadOnlyList<ChatTurn> BuildChatHistory(
        List<FlashcardResponseAiDto> alreadyGenerated)
    {
        if (alreadyGenerated.Count == 0)
            return Array.Empty<ChatTurn>();

        var turns = new List<ChatTurn>(alreadyGenerated.Count * 2);
        foreach (var card in alreadyGenerated.TakeLast(ChatHistoryLimit))
        {
            turns.Add(new ChatTurn(
                "user",
                $"Produce the next flashcard. The previous card had front=\"{card.Front}\"."));
            turns.Add(new ChatTurn(
                "assistant",
                $"{{ \"front\": \"{EscapeForJson(card.Front)}\", \"back\": \"{EscapeForJson(card.Back)}\" }}"));
        }
        return turns;
    }

    private static string BuildUserTurn(int currentIndex, int totalRequested)
    {
        return $"Produce flashcard {currentIndex} of {totalRequested}. Return ONLY the JSON object.";
    }

    private static string EscapeForJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }

    private static string ExtractChunkContent(string? chunkJson)
    {
        if (string.IsNullOrWhiteSpace(chunkJson))
            return string.Empty;

        try
        {
            var chunk = JsonSerializer.Deserialize<ChunkDto>(
                chunkJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (!string.IsNullOrWhiteSpace(chunk?.Content))
                return chunk.Content;
        }
        catch (JsonException)
        {
        }

        return chunkJson;
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
