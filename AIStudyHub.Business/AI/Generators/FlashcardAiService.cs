using AIStudyHub.Business.Interfaces.AI.Generators;
using AIStudyHub.Business.AI.Generators;
using AIStudyHub.Business.AI.Generators.Common;
using AIStudyHub.Business.AI.LLM;
using AIStudyHub.Business.Interfaces.AI.LLM;
using AIStudyHub.Business.Interfaces.AI.Tracking;
using AIStudyHub.Business.Common;
using AIStudyHub.Business.DTOs.AI;
using AIStudyHub.Business.DTOs.Flashcards;
using AIStudyHub.Business.Interfaces.AI.VectorStore;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Exceptions;
using AIStudyHub.Business.Options;
using AIStudyHub.Data.Enums;
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
    private readonly IOpenAIService _openAIService;
    private readonly IVectorStoreService _vectorStoreService;
    private readonly RagOptions _options;
    private readonly ILogger<FlashcardAiService> _logger;
    private readonly ITokenTrackerService _tokenTracker;

    private const int MaxModelCalls = 4;
    private const int EstimatedTokensPerBatch = 1300; // was 1500

    public FlashcardAiService(
        IUnitOfWork unitOfWork,
        IOpenAIService openAIService,
        IVectorStoreService vectorStoreService,
        IOptions<RagOptions> options,
        ILogger<FlashcardAiService> logger,
        ITokenTrackerService tokenTracker)
    {
        _unitOfWork = unitOfWork;
        _openAIService = openAIService;
        _vectorStoreService = vectorStoreService;
        _options = options.Value;
        _logger = logger;
        _tokenTracker = tokenTracker;
    }

    public async Task<FlashcardDeckResponseDto> GenerateFlashcardsAsync(
        Guid documentId,
        CreateFlashcardsViaAiRequestDto request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var accountingOperationId = Guid.NewGuid();

        if (request.NumberOfFlashcards <= 0 || request.NumberOfFlashcards > 20)
            throw new ArgumentOutOfRangeException(nameof(request.NumberOfFlashcards), "Request between 1 and 20 flashcards.");

        var document = await _unitOfWork.Documents.GetByIdAsync(documentId, cancellationToken);
        if (document is null || document.UserId != userId)
            throw new KeyNotFoundException("Document not found.");

        if (document.Status != DocumentStatus.Done)
            throw new InvalidOperationException(
                "Document must finish processing before AI generation.");

        _logger.LogInformation("Generating {Num} flashcards for document {DocId} using OpenAI", request.NumberOfFlashcards, documentId);

        var payloads = await _vectorStoreService.GetPayloadsByDocumentIdAsync(documentId);

        var sortedChunks = payloads
            .OrderBy(p => int.TryParse(p.GetValueOrDefault("chunkIndex", "0"), out var idx) ? idx : 0)
            .Select(p => FixMojibake(p.GetValueOrDefault("text", "")))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        var context = BuildContextFromPayloads(sortedChunks);
        if (string.IsNullOrWhiteSpace(context))
            throw new InvalidOperationException("Document has no processed content.");

        _logger.LogInformation("Flashcard context length: {Length} chars from {ChunkCount} chunks",
            context.Length, sortedChunks.Count);

        var estimatedTokens = MaxModelCalls * EstimatedTokensPerBatch;
        if (!await _tokenTracker.HasQuotaAsync(userId, estimatedTokens, cancellationToken))
        {
            var (current, limit) = await _tokenTracker.GetUsageInfoAsync(userId, cancellationToken);
            throw new QuotaExceededException(current, limit, estimatedTokens);
        }

        var flashcards = new List<FlashcardResponseAiDto>(request.NumberOfFlashcards);
        var seenFronts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        const int batchSize = 20;

        var totalInputTokens = 0;
        var totalOutputTokens = 0;

        try
        {
            for (var modelCall = 1;
                 modelCall <= MaxModelCalls
                 && flashcards.Count < request.NumberOfFlashcards;
                 modelCall++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var remaining =
                    request.NumberOfFlashcards - flashcards.Count;
                var wantThisBatch =
                    Math.Min(batchSize, remaining);

                var (batchCards, inputTokens, outputTokens) =
                    await RunBatchWithTrackingAsync(
                        context,
                        flashcards,
                        wantThisBatch,
                        modelCall);

                totalInputTokens += inputTokens;
                totalOutputTokens += outputTokens;

                var added = 0;
                foreach (var card in batchCards)
                {
                    if (flashcards.Count
                        >= request.NumberOfFlashcards)
                    {
                        break;
                    }

                    var normalizedFront = new string(
                            card.Front
                                .Where(char.IsLetterOrDigit)
                                .ToArray())
                        .ToLowerInvariant();
                    if (normalizedFront.Length < 5)
                        continue;

                    if (!seenFronts.Add(normalizedFront))
                    {
                        _logger.LogInformation(
                            "Flashcard model call {ModelCall} produced duplicate, skipping",
                            modelCall);
                        continue;
                    }

                    flashcards.Add(card);
                    added++;
                }

                _logger.LogInformation(
                    "Flashcard model call {ModelCall}: wanted {Want}, accepted {Accepted}, total {Total}/{Requested}",
                    modelCall,
                    wantThisBatch,
                    added,
                    flashcards.Count,
                    request.NumberOfFlashcards);
            }
        }
        finally
        {
            await RecordConsumedTokensAsync(
                accountingOperationId,
                userId,
                documentId,
                totalInputTokens,
                totalOutputTokens);
        }

        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            "Finished flashcard generation: {Got}/{Requested}",
            flashcards.Count,
            request.NumberOfFlashcards);

        if (flashcards.Count != request.NumberOfFlashcards)
        {
            throw new ExactGenerationCountException(
                request.NumberOfFlashcards,
                flashcards.Count);
        }

        // Always create a brand-new deck so each generation call is its own study set.
        var existingCount = await _unitOfWork.FlashcardDecks
            .Query()
            .CountAsync(d => d.DocumentId == documentId, cancellationToken);

        var deckName = $"Flashcard {existingCount + 1}";

        var deck = new AIStudyHub.Data.Entities.FlashcardDeck
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            Name = deckName,
            CreatedAt = DateTime.UtcNow
        };
        await _unitOfWork.FlashcardDecks.AddAsync(deck, cancellationToken);

        var entities = flashcards.Select(f => new AIStudyHub.Data.Entities.Flashcard
        {
            DeckId = deck.Id,
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
            e.DeckId,
            e.Front,
            e.Back,
            e.CreatedAt,
            e.UpdatedAt
        )).ToList();

        return new FlashcardDeckResponseDto(deck.Id, deck.Name, result);
    }

    private async Task<(List<FlashcardResponseAiDto> cards, int inputTokens, int outputTokens)> RunBatchWithTrackingAsync(
        string context,
        IReadOnlyList<FlashcardResponseAiDto> existing,
        int wantThisBatch,
        int modelCall)
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

        TokenUsageResult usageResult;
        try
        {
            usageResult = await _openAIService.SendMessageWithUsageAsync(prompt, 0.2f);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Flashcard model call {ModelCall}: AI call failed",
                modelCall);
            return (new List<FlashcardResponseAiDto>(), 0, 0);
        }

        try
        {
            return (
                ParseFlashcardArray(usageResult.Text),
                usageResult.InputTokens,
                usageResult.OutputTokens);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Flashcard model call {ModelCall}: parse failed",
                modelCall);
            return (
                new List<FlashcardResponseAiDto>(),
                usageResult.InputTokens,
                usageResult.OutputTokens);
        }
    }

    private Task RecordConsumedTokensAsync(
        Guid operationId,
        Guid userId,
        Guid documentId,
        int inputTokens,
        int outputTokens)
    {
        return _tokenTracker.RecordGenerationUsageAsync(
            operationId,
            userId,
            documentId,
            inputTokens,
            outputTokens,
            "GenerateFlashcards");
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
        return BatchParsingHelpers.ParseArrayStreaming(
            array,
            arr => ExtractCardsFromArrayElement(arr).AsEnumerable());
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
        return BatchGeneratorBase<object>.ExtractBalanced(text, open, close) is { } result
            ? result
            : null;
    }

    private static string BuildContextFromPayloads(List<Dictionary<string, string>> payloads)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var payload in payloads)
        {
            if (payload.TryGetValue("text", out var text) && !string.IsNullOrWhiteSpace(text))
            {
                sb.AppendLine(text);
                sb.AppendLine();
            }
            if (sb.Length > 20_000) return sb.ToString();
        }
        return sb.ToString();
    }

    private static string Clean(string s) => TextSanitizer.CleanBracketedReferences(s);
}
