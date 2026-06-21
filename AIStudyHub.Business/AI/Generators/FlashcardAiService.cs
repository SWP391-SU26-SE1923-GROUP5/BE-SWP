using AIStudyHub.Business.Interfaces.AI.Generators;
using AIStudyHub.Business.AI.Generators;
using AIStudyHub.Business.AI.LLM;
using AIStudyHub.Business.Interfaces.AI.LLM;
using AIStudyHub.Business.DTOs.Flashcards;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Options;
using AIStudyHub.Data.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AIStudyHub.Business.AI.Generators;

public sealed class FlashcardAiService : IFlashcardAiService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalAIService _localAIService;
    private readonly Microsoft.KernelMemory.IKernelMemory _memory;
    private readonly RagOptions _options;
    private readonly ILogger<FlashcardAiService> _logger;

    // Hard caps so a single misbehaving model can't burn the request budget.
    private const int MaxAttemptsPerCard = 3;
    private const int MaxTotalAttempts = 80;

    public FlashcardAiService(
        IUnitOfWork unitOfWork,
        ILocalAIService localAIService,
        Microsoft.KernelMemory.IKernelMemory memory,
        IOptions<RagOptions> options,
        ILogger<FlashcardAiService> logger)
    {
        _unitOfWork = unitOfWork;
        _localAIService = localAIService;
        _memory = memory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<FlashcardResponseDto>> GenerateFlashcardsAsync(
        Guid documentId,
        CreateFlashcardsViaAiRequestDto request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (request.NumberOfFlashcards <= 0 || request.NumberOfFlashcards > 20)
            throw new ArgumentOutOfRangeException(nameof(request.NumberOfFlashcards), "Request between 1 and 20 flashcards.");

        var document = await _unitOfWork.Documents.GetByIdAsync(documentId);
        if (document == null)
            throw new KeyNotFoundException("Document not found.");

        if (document.UserId != userId)
            throw new UnauthorizedAccessException("You do not have permission to access this document.");

        _logger.LogInformation("Generating {Num} flashcards for document {DocId} using OpenAI", request.NumberOfFlashcards, documentId);

        var searchResult = await _memory.SearchAsync(
            "",
            filter: Microsoft.KernelMemory.MemoryFilters.ByDocument(documentId.ToString()),
            limit: 1000,
            cancellationToken: cancellationToken);

        var context = BuildContext(searchResult.Results);
        var flashcards = new List<FlashcardResponseAiDto>(request.NumberOfFlashcards);
        var seenFronts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var remaining = request.NumberOfFlashcards;
        var batchNumber = 0;
        var maxBatches = request.NumberOfFlashcards * 3;
        var consecutiveZeroAdded = 0;
        const int batchSize = 5;

        while (remaining > 0 && batchNumber < maxBatches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            batchNumber++;
            var wantThisBatch = Math.Min(batchSize, remaining + 2); // Ask for a bit more

            var batchCards = await RunBatchWithRetryAsync(
                context, flashcards, wantThisBatch, batchNumber, cancellationToken);

            var added = 0;
            foreach (var card in batchCards)
            {
                if (flashcards.Count >= request.NumberOfFlashcards) break;
                
                // Aggressively normalize front to catch slight variations
                var normalizedFront = new string(card.Front.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
                if (normalizedFront.Length < 5) continue;

                if (!seenFronts.Add(normalizedFront))
                {
                    _logger.LogInformation("Flashcard batch {Batch} produced duplicate, skipping", batchNumber);
                    continue;
                }

                flashcards.Add(card);
                added++;
            }

            _logger.LogInformation("Flashcard batch {Batch}: wanted {Want}, accepted {Accepted}, total {Total}/{Requested}",
                batchNumber, wantThisBatch, added, flashcards.Count, request.NumberOfFlashcards);

            if (added == 0)
            {
                consecutiveZeroAdded++;
                if (consecutiveZeroAdded >= 3)
                {
                    _logger.LogWarning("Aborting flashcard generation after 3 consecutive zero-yield batches.");
                    break;
                }
            }
            else
            {
                consecutiveZeroAdded = 0;
            }

            remaining = request.NumberOfFlashcards - flashcards.Count;
        }

        _logger.LogInformation(
            "Finished flashcard generation: {Got}/{Requested} after {Attempts} batches",
            flashcards.Count, request.NumberOfFlashcards, batchNumber);

        // Persist to database
        var entities = flashcards.Select(f => new AIStudyHub.Data.Entities.Flashcard
        {
            DocumentId = documentId,
            Front = f.Front,
            Back = f.Back
        }).ToList();

        foreach (var entity in entities)
        {
            await _unitOfWork.Flashcards.AddAsync(entity, cancellationToken);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var result = entities.Select(e => new FlashcardResponseDto(
            e.Id,
            e.DocumentId,
            e.Front,
            e.Back,
            e.CreatedAt,
            e.UpdatedAt
        )).ToList();

        return result;
    }

    private async Task<List<FlashcardResponseAiDto>> RunBatchWithRetryAsync(
        string context,
        IReadOnlyList<FlashcardResponseAiDto> existing,
        int wantThisBatch,
        int batchNumber,
        CancellationToken cancellationToken)
    {
        var avoidBlock = existing.Count == 0
            ? string.Empty
            : "\n\nDo NOT repeat or paraphrase any of these existing flashcards:\n" +
              string.Join("\n", existing.Select(x => $"- {x.Front}"));

        var prompt = $$"""
Read the following TEXT. Your task is to extract EXACTLY {{wantThisBatch}} different facts from this TEXT and convert them into study flashcards.

TEXT:
{{context}}{{avoidBlock}}

Generate the flashcards as a JSON array of objects.
Do not write anything else. No prose. No markdown. Just the JSON array.

FORMAT:
[
  { "front": "Write a question based on the TEXT here?", "back": "Write the short answer based on the TEXT here." },
  { "front": "Write another question from the TEXT here?", "back": "Write the short answer here." }
]

RULES:
- "front" MUST be a QUESTION ending with '?'.
- "back" MUST be the ANSWER, written as a short factual statement.
- "back" MUST NOT contain a question mark.
- All facts MUST come from the TEXT above. Do not invent information.
- Output ONLY the JSON array. Start with '[' and end with ']'.
""";

        const int maxAttempts = 2;
        var best = new List<FlashcardResponseAiDto>();

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string aiText;
            try
            {
                aiText = await _localAIService.SendMessageAsync(prompt, 0.2f);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Flashcard batch {Batch} attempt {Attempt}: AI call failed", batchNumber, attempt);
                continue;
            }

            var parsed = ParseFlashcardArray(aiText);
            
            if (parsed.Count > best.Count)
                best = parsed;

            if (parsed.Count >= Math.Max(1, wantThisBatch / 2))
                return parsed;

            _logger.LogWarning("Flashcard batch {Batch} attempt {Attempt}: only {Got}/{Want} cards, retrying", batchNumber, attempt, parsed.Count, wantThisBatch);
        }

        return best;
    }

    private static List<FlashcardResponseAiDto> ParseFlashcardArray(string aiText)
    {
        if (string.IsNullOrWhiteSpace(aiText)) return new List<FlashcardResponseAiDto>();

        var text = aiText.Trim();
        text = Regex.Replace(text, @"^```(?:json)?\s*", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\s*```\s*$", "", RegexOptions.IgnoreCase);

        var arraySlice = ExtractBalancedObject(text, '[', ']');
        if (arraySlice is null) return new List<FlashcardResponseAiDto>();

        try
        {
            var sanitized = Regex.Replace(arraySlice, @"[\u0000-\u0008\u000B\u000C\u000E-\u001F]", "");
            using var doc = JsonDocument.Parse(sanitized, new JsonDocumentOptions { AllowTrailingCommas = true });
            
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return new List<FlashcardResponseAiDto>();

            return ExtractCardsFromArrayElement(doc.RootElement);
        }
        catch (JsonException)
        {
            // If the array is malformed, fall back to streaming parser (extracts objects one by one)
            return ParseArrayStreaming(arraySlice);
        }
    }

    private static List<FlashcardResponseAiDto> ExtractCardsFromArrayElement(JsonElement array)
    {
        var result = new List<FlashcardResponseAiDto>();
        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object) continue;
            if (!element.TryGetProperty("front", out var f) || f.ValueKind != JsonValueKind.String) continue;
            if (!element.TryGetProperty("back", out var b) || b.ValueKind != JsonValueKind.String) continue;

            var front = Clean(f.GetString() ?? "");
            var back = Clean(b.GetString() ?? "");

            var (finalFront, finalBack) = EnforceFrontQuestionBackAnswer(front, back);
            if (string.IsNullOrWhiteSpace(finalFront) || string.IsNullOrWhiteSpace(finalBack)) continue;
            
            // Be more lenient with LooksLikeQuestion to accept more cards from weak models
            if (!LooksLikeQuestion(finalFront) && !finalFront.EndsWith('?')) finalFront += "?";

            result.Add(new FlashcardResponseAiDto(finalFront, finalBack));
        }
        return result;
    }

    private static List<FlashcardResponseAiDto> ParseArrayStreaming(string array)
    {
        var sanitized = Regex.Replace(array, @"[\u0000-\u0008\u000B\u000C\u000E-\u001F]", "");
        var result = new List<FlashcardResponseAiDto>();
        var i = 0;
        
        while (i < sanitized.Length)
        {
            while (i < sanitized.Length && (char.IsWhiteSpace(sanitized[i]) || sanitized[i] == ',' || sanitized[i] == '[' || sanitized[i] == ']'))
                i++;
            if (i >= sanitized.Length) break;

            if (sanitized[i] != '{') { i++; continue; }

            var objStart = i;
            var depth = 0;
            var inString = false;
            var escape = false;
            var found = false;
            
            for (; i < sanitized.Length; i++)
            {
                var c = sanitized[i];
                if (inString)
                {
                    if (escape) { escape = false; continue; }
                    if (c == '\\') { escape = true; continue; }
                    if (c == '"') inString = false;
                    continue;
                }
                if (c == '"') { inString = true; continue; }
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) { found = true; i++; break; }
                }
            }

            if (!found) break;

            var slice = sanitized.Substring(objStart, i - objStart);
            try
            {
                using var doc = JsonDocument.Parse(slice, new JsonDocumentOptions { AllowTrailingCommas = true });
                result.AddRange(ExtractCardsFromArrayElement(WrapSingleObject(doc.RootElement.Clone())));
            }
            catch (JsonException)
            {
                // Skip broken element
            }
        }
        return result;
    }

    private static JsonElement WrapSingleObject(JsonElement obj)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartArray();
            w.WriteRawValue(obj.GetRawText(), skipInputValidation: true);
            w.WriteEndArray();
        }
        return JsonDocument.Parse(ms.ToArray()).RootElement.Clone();
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

    private static string BuildContext(IEnumerable<Microsoft.KernelMemory.Citation> citations)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var citation in citations)
        {
            foreach (var partition in citation.Partitions)
            {
                if (string.IsNullOrWhiteSpace(partition.Text)) continue;
                sb.AppendLine(partition.Text);
                sb.AppendLine();
                if (sb.Length > 30_000) return sb.ToString();
            }
        }
        return sb.ToString();
    }

    private static string Clean(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        return Regex.Replace(s, @"\s*\[[^\]]+\]", "").Trim();
    }
}
