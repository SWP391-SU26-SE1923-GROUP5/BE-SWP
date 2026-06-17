using AIStudyHub.Business.DTOs.Flashcards;
using AIStudyHub.Business.DTOs.Rag;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Options;
using AIStudyHub.Data.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AIStudyHub.Business.Services;

public sealed class FlashcardAiService : IFlashcardAiService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalAIService _localAIService;
    private readonly RagOptions _options;
    private readonly ILogger<FlashcardAiService> _logger;

    // Hard caps so a single misbehaving model can't burn the request budget.
    private const int MaxAttemptsPerCard = 3;
    private const int MaxTotalAttempts = 80;

    public FlashcardAiService(
        IUnitOfWork unitOfWork,
        ILocalAIService localAIService,
        IOptions<RagOptions> options,
        ILogger<FlashcardAiService> logger)
    {
        _unitOfWork = unitOfWork;
        _localAIService = localAIService;
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
            documentId, cancellationToken);
        if (document is null)
            throw new KeyNotFoundException("Document not found");

        var chunks = await _unitOfWork.DocumentChunks
            .Query()
            .Where(c => c.DocumentId == documentId)
            .OrderBy(c => c.OrderIndex)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

<<<<<<< HEAD
        var context = BuildContext(chunks);
        var flashcards = new List<FlashcardResponseAiDto>(request.NumberOfFlashcards);
        var seenFronts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
=======
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
>>>>>>> b2820b1166319b4413a27b83e4366c51cf8c1b80

        var totalAttempts = 0;
        var consecutiveFailures = 0;

        // Persistent loop: keep trying until we hit the requested count OR
        // burn the global attempt budget. Same idea as QuizAiService.
        while (flashcards.Count < request.NumberOfFlashcards
               && totalAttempts < MaxTotalAttempts)
        {
<<<<<<< HEAD
            cancellationToken.ThrowIfCancellationRequested();
            totalAttempts++;

            var card = await TryGenerateOneCardAsync(
                context, flashcards, totalAttempts, cancellationToken);

            if (card is null)
            {
                consecutiveFailures++;
                _logger.LogWarning(
                    "Flashcard attempt {Attempt} failed (consecutive={Consec})",
                    totalAttempts, consecutiveFailures);

                // If the model is completely broken, give up rather than spin.
                if (consecutiveFailures >= MaxAttemptsPerCard * 2)
                {
                    _logger.LogError(
                        "Aborting flashcard generation after {Consec} consecutive failures",
                        consecutiveFailures);
                    break;
                }
                continue;
            }

            if (!seenFronts.Add(card.Front))
            {
                _logger.LogInformation(
                    "Flashcard attempt {Attempt} produced duplicate front, skipping",
                    totalAttempts);
                continue;
            }

            consecutiveFailures = 0;
            flashcards.Add(card);
            _logger.LogInformation(
                "Flashcard {Got}/{Requested} generated (attempt {Attempt})",
                flashcards.Count, request.NumberOfFlashcards, totalAttempts);
=======
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
>>>>>>> b2820b1166319b4413a27b83e4366c51cf8c1b80
        }

        _logger.LogInformation(
            "Finished flashcard generation: {Got}/{Requested} after {Attempts} attempts",
            flashcards.Count, request.NumberOfFlashcards, totalAttempts);

        return new FlashcardsAiResponseDto(flashcards);
    }

<<<<<<< HEAD
    private async Task<FlashcardResponseAiDto?> TryGenerateOneCardAsync(
        string context,
        IReadOnlyList<FlashcardResponseAiDto> existing,
        int attemptNumber,
        CancellationToken cancellationToken)
    {
        var avoidBlock = existing.Count == 0
            ? string.Empty
            : "\n\nDo NOT repeat or paraphrase any of these existing flashcards:\n" +
              string.Join("\n", existing.Select(x => $"- {x.Front}"));

        var prompt = $$"""
You are a JSON API. You generate study flashcards from a CONTEXT.

Return ONLY a single valid JSON object. No markdown, no prose, no code fences, no commentary.

CONTRACT — STRICT AND NON-NEGOTIABLE:
- "front" MUST be a QUESTION. It is what the student sees first and tries to answer.
- "back"  MUST be the ANSWER, written as a short factual statement (1–2 sentences).
- The card is read in study mode: front = prompt, back = reveal. Do not invert this.

HARD RULES:
- "front" MUST end with "?" OR start with one of: What, Who, When, Where, Why, How, Which, Define, Explain, Describe, List, Name, In what, On what, According to the context.
- "front" length: 5–200 characters. "back" length: 1–500 characters.
- "back" MUST NOT contain a question mark.
- Both fields must be NON-EMPTY strings.
- All facts MUST come from CONTEXT.
- Output ONLY the JSON object. Start with '{' and end with '}'. Nothing else.

EXAMPLE (correct shape, not from CONTEXT):
{ "front": "What is photosynthesis?", "back": "The process by which plants convert light energy into chemical energy stored in glucose." }

EXAMPLE (wrong shape — do NOT produce this):
{ "front": "Photosynthesis is the process by which plants convert light energy into chemical energy.", "back": "What is photosynthesis?" }
^ This is INVERTED. Never do this.

CONTEXT:
{{context}}{{avoidBlock}}
""";

        var aiText = await _localAIService.SendMessageAsync(prompt, 0.2f);

        var parsed = TryParseCard(aiText);
        if (parsed is null) return null;

        // Enforce front=question, back=answer. The 1B model frequently swaps.
        var (front, back) = EnforceFrontQuestionBackAnswer(parsed.Value.front, parsed.Value.back);
        if (string.IsNullOrWhiteSpace(front) || string.IsNullOrWhiteSpace(back))
            return null;

        // Final sanity: front must be question-shaped.
        if (!LooksLikeQuestion(front)) return null;
        if (back.Contains('?')) return null;

        return new FlashcardResponseAiDto(front, back);
    }

    private static (string front, string back)? TryParseCard(string aiText)
    {
        if (string.IsNullOrWhiteSpace(aiText)) return null;

        var text = aiText.Trim();
        text = Regex.Replace(text, @"^```(?:json)?\s*", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\s*```\s*$", "", RegexOptions.IgnoreCase);

        // Find the first balanced {...} in the response. The model may wrap
        // the JSON in prose on a bad day.
        var slice = ExtractBalancedObject(text, '{', '}');
        if (slice is null) return null;

        var sanitized = Regex.Replace(
            slice, @"[\u0000-\u0008\u000B\u000C\u000E-\u001F]", "");

        try
        {
            using var doc = JsonDocument.Parse(
                sanitized,
                new JsonDocumentOptions { AllowTrailingCommas = true });
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;

            if (!doc.RootElement.TryGetProperty("front", out var f)
                || f.ValueKind != JsonValueKind.String) return null;
            if (!doc.RootElement.TryGetProperty("back", out var b)
                || b.ValueKind != JsonValueKind.String) return null;

            return (Clean(f.GetString() ?? ""), Clean(b.GetString() ?? ""));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static (string front, string back) EnforceFrontQuestionBackAnswer(
        string front, string back)
    {
        var frontIsQuestion = LooksLikeQuestion(front);
        var backIsQuestion = LooksLikeQuestion(back);

        // If the model inverted them, swap.
        if (!frontIsQuestion && backIsQuestion)
            return (back, front);

        return (front, back);
    }

    private static bool LooksLikeQuestion(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        var t = s.Trim();

        if (t.EndsWith('?')) return true;

        // Allow titles with no '?' only if they start with a question word.
        // This keeps the model honest while still accepting "Define: X." etc.
        var prefixes = new[]
        {
            "what", "who", "when", "where", "why", "how", "which",
            "define", "explain", "describe", "list", "name",
            "in what", "on what", "according to", "true or false"
        };
        foreach (var p in prefixes)
        {
            if (t.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static string? ExtractBalancedObject(string text, char open, char close)
    {
        var startIdx = text.IndexOf(open);
        if (startIdx < 0) return null;
=======
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
>>>>>>> b2820b1166319b4413a27b83e4366c51cf8c1b80

        var depth = 0;
        var inString = false;
        var escape = false;
<<<<<<< HEAD
        for (var i = startIdx; i < text.Length; i++)
        {
            var c = text[i];
            if (inString)
            {
                if (escape) { escape = false; continue; }
                if (c == '\\') { escape = true; continue; }
                if (c == '"') inString = false;
                continue;
            }
            if (c == '"') { inString = true; continue; }
            if (c == open) depth++;
            else if (c == close)
            {
                depth--;
                if (depth == 0) return text.Substring(startIdx, i - startIdx + 1);
            }
        }
        return null;
    }

    private static string BuildContext(IReadOnlyList<Data.Entities.DocumentChunk> chunks)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in chunks)
        {
            if (string.IsNullOrWhiteSpace(c.ChunkJson)) continue;
            sb.AppendLine(c.ChunkJson);
            sb.AppendLine();
            if (sb.Length > 30_000) break;
        }
        return sb.ToString();
    }

    private static string Clean(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        return Regex.Replace(s, @"\s*\[[^\]]+\]", "").Trim();
    }
=======

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

>>>>>>> b2820b1166319b4413a27b83e4366c51cf8c1b80
}
