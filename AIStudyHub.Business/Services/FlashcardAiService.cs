using AIStudyHub.Business.DTOs.Flashcards;
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
        if (request.NumberOfFlashcards <= 0 || request.NumberOfFlashcards > 20)
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

        var context = BuildContext(chunks);
        var flashcards = new List<FlashcardResponseAiDto>(request.NumberOfFlashcards);
        var seenFronts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var totalAttempts = 0;
        var consecutiveFailures = 0;

        // Persistent loop: keep trying until we hit the requested count OR
        // burn the global attempt budget. Same idea as QuizAiService.
        while (flashcards.Count < request.NumberOfFlashcards
               && totalAttempts < MaxTotalAttempts)
        {
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
        }

        _logger.LogInformation(
            "Finished flashcard generation: {Got}/{Requested} after {Attempts} attempts",
            flashcards.Count, request.NumberOfFlashcards, totalAttempts);

        return new FlashcardsAiResponseDto(flashcards);
    }

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

        var depth = 0;
        var inString = false;
        var escape = false;
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
}
