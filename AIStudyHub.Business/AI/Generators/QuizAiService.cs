using AIStudyHub.Business.Interfaces.AI.Generators;
using AIStudyHub.Business.AI.Generators;
using AIStudyHub.Business.AI.Generators.Common;
using AIStudyHub.Business.Interfaces.AI.VectorStore;
using AIStudyHub.Business.AI.LLM;
using AIStudyHub.Business.Interfaces.AI.LLM;
using AIStudyHub.Business.Interfaces.AI.Tracking;
using AIStudyHub.Business.Common;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AIStudyHub.Business.DTOs.Quizzes;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Exceptions;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;
using AIStudyHub.Data.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AIStudyHub.Business.AI.Generators;

public sealed class QuizAiService : IQuizAiService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOpenAIService _openAiService;
    private readonly IVectorStoreService _vectorStoreService;
    private readonly ILogger<QuizAiService> _logger;
    private readonly ITokenTrackerService _tokenTracker;

    private const int EstimatedTokensPerBatch = 1800; // was 2000

    public QuizAiService(
        IUnitOfWork unitOfWork,
        IOpenAIService openAiService,
        IVectorStoreService vectorStoreService,
        ILogger<QuizAiService> logger,
        ITokenTrackerService tokenTracker)
    {
        _unitOfWork = unitOfWork;
        _openAiService = openAiService;
        _vectorStoreService = vectorStoreService;
        _logger = logger;
        _tokenTracker = tokenTracker;
    }

    public async Task<QuizResponseDto> GenerateAndPersistQuizAsync(
        Guid documentId,
        CreateQuizRequestViaAIDto request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (request.numberOfQuestions <= 0 || request.numberOfQuestions > 20)
            throw new ArgumentOutOfRangeException(
                nameof(request.numberOfQuestions),
                "Number of questions must be between 1 and 20.");

        var document = await _unitOfWork.Documents.GetByIdAsync(documentId, cancellationToken);
        if (document is null)
            throw new KeyNotFoundException("Document not found");

        // Check AI token quota before processing
        var estimatedBatches = (int)Math.Ceiling((double)request.numberOfQuestions / 15);
        var estimatedTokens = estimatedBatches * EstimatedTokensPerBatch;
        if (!await _tokenTracker.HasQuotaAsync(userId, estimatedTokens, cancellationToken))
        {
            var (current, limit) = await _tokenTracker.GetUsageInfoAsync(userId, cancellationToken);
            throw new QuotaExceededException(current, limit, estimatedTokens);
        }

        var payloads = await _vectorStoreService.GetPayloadsByDocumentIdAsync(documentId);

        var sortedChunks = payloads
            .OrderBy(p => int.TryParse(p.GetValueOrDefault("chunkIndex", "0"), out var idx) ? idx : 0)
            .Select(p => FixMojibake(p.GetValueOrDefault("text", "")))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        var context = string.Join("\n\n", sortedChunks);

        if (context.Length > 20000)
        {
            context = context.Substring(0, 20000);
            var lastPeriod = context.LastIndexOf('.');
            if (lastPeriod > 10000)
            {
                context = context.Substring(0, lastPeriod + 1);
            }
        }

        _logger.LogInformation("Quiz context length: {Length} chars from {ChunkCount} chunks",
            context.Length, sortedChunks.Count);

        const int batchSize = 15;
        var allQuestions = new List<AiGeneratedQuestionDto>(request.numberOfQuestions);
        var seenTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var remaining = request.numberOfQuestions;
        var batchNumber = 0;
        var maxBatches = request.numberOfQuestions * 3;
        var consecutiveZeroAdded = 0;
        var runningTitle = string.Empty;

        // Track total tokens used
        int totalInputTokens = 0;
        int totalOutputTokens = 0;

        while (remaining > 0 && batchNumber < maxBatches)
        {
            batchNumber++;
            var wantThisBatch = Math.Min(batchSize, remaining + 2);

            var prompt = BuildBatchPrompt(
                wantThisBatch,
                context,
                allQuestions,
                startingPosition: allQuestions.Count + 1);

            var (batchQuestions, inputTokens, outputTokens) = await RunBatchWithRetryWithTrackingAsync(
                prompt, wantThisBatch, batchNumber, cancellationToken);

            totalInputTokens += inputTokens;
            totalOutputTokens += outputTokens;

            var added = 0;
            foreach (var q in batchQuestions)
            {
                if (allQuestions.Count >= request.numberOfQuestions)
                    break;

                var normalized = NormalizeQuestion(q, allQuestions.Count + 1);
                if (normalized is null) continue;

                var normalizedTitleText = new string(normalized.QuestionTitle.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
                if (normalizedTitleText.Length < 5) continue;

                if (!seenTitles.Add(normalizedTitleText))
                {
                    continue;
                }

                allQuestions.Add(normalized);
                added++;
            }

            _logger.LogInformation(
                "Quiz batch {Batch}: wanted {Want}, parsed {Parsed}, accepted {Accepted}, total {Total}/{Requested}",
                batchNumber, wantThisBatch, batchQuestions.Count, added, allQuestions.Count, request.numberOfQuestions);

            if (added == 0)
            {
                consecutiveZeroAdded++;
                if (consecutiveZeroAdded >= 3)
                {
                    _logger.LogWarning("Aborting quiz generation after 3 consecutive zero-yield batches.");
                    break;
                }
            }
            else
            {
                consecutiveZeroAdded = 0;
            }

            remaining = request.numberOfQuestions - allQuestions.Count;
        }

        if (allQuestions.Count == 0)
        {
            _logger.LogWarning(
                "No quiz questions generated for document {DocumentId}", documentId);
            throw new KeyNotFoundException("AI could not generate any valid questions from the document.");
        }

        runningTitle = string.IsNullOrWhiteSpace(runningTitle)
            ? $"Quiz on {document.Title}"
            : runningTitle;

        var result = new AiGeneratedQuizResponseDto(runningTitle, allQuestions);

        var quiz = await PersistQuizAsync(documentId, document.Title, result, cancellationToken);

        _logger.LogInformation(
            "Generated {Count}/{Requested} quiz questions for document {DocumentId}",
            allQuestions.Count, request.numberOfQuestions, documentId);

        // Record token usage
        if (totalInputTokens > 0 || totalOutputTokens > 0)
        {
            await _tokenTracker.RecordUsageAsync(userId, totalInputTokens, totalOutputTokens, "quiz", cancellationToken);
        }

        return new QuizResponseDto(
            quiz.Id,
            quiz.DocumentId,
            quiz.Title,
            quiz.CreatedAt,
            quiz.UpdatedAt
        );
    }

    // Blacklist of placeholder phrases that the LLM tends to copy verbatim.
    private static readonly HashSet<string> PlaceholderBlacklist = new(StringComparer.OrdinalIgnoreCase)
    {
        "write the correct answer here",
        "write a wrong answer here",
        "write another wrong answer here",
        "write a third wrong answer here",
        "write question 1 based on the text here",
        "write a short topic title here",
        "đáp án chính xác",
        "đáp án sai thứ nhất",
        "đáp án sai thứ hai",
        "đáp án sai thứ ba",
        "một câu hỏi hoàn chỉnh dựa trên nội dung là gì",
        "chủ đề bài kiểm tra",
        "vui lòng chọn đáp án chính xác",
        "sau đây là một số câu trả lời",
    };

    private static bool IsPlaceholderText(string text)
    {
        var cleaned = text.Trim().TrimEnd('?', '.', '!').Trim();
        return PlaceholderBlacklist.Contains(cleaned);
    }

    private static string BuildBatchPrompt(
        int count,
        string context,
        IReadOnlyCollection<AiGeneratedQuestionDto> alreadyGenerated,
        int startingPosition)
    {
        var avoidBlock = alreadyGenerated.Count == 0
            ? string.Empty
            : "\n\nDo NOT repeat or paraphrase any of these existing questions:\n" +
              string.Join("\n", alreadyGenerated.Select(q => $"- {q.QuestionTitle}"));

        return $$"""
You are a teacher. Read the TEXT below and create a quiz with EXACTLY {{count}} multiple-choice questions.
Each question must END with a question mark (?).
Each question must have EXACTLY 4 answer options.
Only 1 answer is correct per question.
Write in the SAME language as the TEXT.

TEXT:
{{context}}{{avoidBlock}}

Output ONLY valid JSON, nothing else. Use this exact structure:
{"quizTitle":"...","questions":[{"questionTitle":"What is ...?","questionType":"SingleChoice","position":{{startingPosition}},"answers":[{"selectedOption":"correct answer text","isCorrect":true},{"selectedOption":"wrong 1","isCorrect":false},{"selectedOption":"wrong 2","isCorrect":false},{"selectedOption":"wrong 3","isCorrect":false}]}]}

IMPORTANT:
- Every questionTitle MUST end with ?
- Every answer must be a real fact or plausible statement from the TEXT
- Do NOT copy placeholder words like "correct answer text" or "wrong 1"
- position starts at {{startingPosition}} and increments by 1
""";
    }

    private async Task<(List<AiGeneratedQuestionDto> questions, int inputTokens, int outputTokens)> RunBatchWithRetryWithTrackingAsync(
        string prompt,
        int wantThisBatch,
        int batchNumber,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 2;
        var best = new List<AiGeneratedQuestionDto>();
        var bestInputTokens = 0;
        var bestOutputTokens = 0;
        var lastAttemptInputTokens = 0;
        var lastAttemptOutputTokens = 0;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string aiText;
            int inputTokens = 0;
            int outputTokens = 0;
            try
            {
                var usageResult = await _openAiService.SendMessageWithUsageAsync(prompt, 0.2f);
                aiText = usageResult.Text;
                inputTokens = usageResult.InputTokens;
                outputTokens = usageResult.OutputTokens;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex, "Quiz batch {Batch} attempt {Attempt}: AI call failed",
                    batchNumber, attempt);
                continue;
            }

            List<AiGeneratedQuestionDto> parsed;
            try
            {
                parsed = ParseQuizPayload(aiText);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex, "Quiz batch {Batch} attempt {Attempt}: parse failed",
                    batchNumber, attempt);
                continue;
            }

            lastAttemptInputTokens = inputTokens;
            lastAttemptOutputTokens = outputTokens;

            if (parsed.Count > best.Count)
            {
                best = parsed;
                bestInputTokens = inputTokens;
                bestOutputTokens = outputTokens;
            }

            if (parsed.Count >= Math.Max(1, wantThisBatch / 2))
                return (parsed, inputTokens, outputTokens);

            _logger.LogWarning(
                "Quiz batch {Batch} attempt {Attempt}: only {Got}/{Want} questions, retrying",
                batchNumber, attempt, parsed.Count, wantThisBatch);
        }

        return (best, lastAttemptInputTokens, lastAttemptOutputTokens);
    }

    private static List<AiGeneratedQuestionDto> ParseQuizPayload(string aiText)
    {
        if (string.IsNullOrWhiteSpace(aiText))
            return new List<AiGeneratedQuestionDto>();

        var text = aiText.Trim();
        text = Regex.Replace(text, @"^```(?:json)?\s*", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\s*```\s*$", "", RegexOptions.IgnoreCase);

        var objSlice = ExtractBalancedObject(text, '{', '}');
        if (objSlice is null)
        {
            // Fall back to array-only shape: {"questions":[...]}
            var arraySlice = ExtractBalancedObject(text, '[', ']');
            if (arraySlice is null) return new List<AiGeneratedQuestionDto>();
            return ExtractQuestionsFromArrayText(arraySlice);
        }

        var questions = new List<AiGeneratedQuestionDto>();
        try
        {
            var sanitized = Regex.Replace(
                objSlice, @"[\u0000-\u0008\u000B\u000C\u000E-\u001F]", "");

            using var doc = JsonDocument.Parse(
                sanitized,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                });

            if (doc.RootElement.ValueKind != JsonValueKind.Object) return new List<AiGeneratedQuestionDto>();
            if (!doc.RootElement.TryGetProperty("questions", out var qArr)
                || qArr.ValueKind != JsonValueKind.Array)
            {
                return new List<AiGeneratedQuestionDto>();
            }

            questions.AddRange(ExtractQuestionsFromArrayElement(qArr));
        }
        catch (JsonException)
        {
            // Top-level object malformed → try recovery on the questions array.
            return ExtractQuestionsFromArrayText(objSlice);
        }
        return questions;
    }

    private static List<AiGeneratedQuestionDto> ExtractQuestionsFromArrayText(string text)
    {
        var sanitized = Regex.Replace(
            text, @"[\u0000-\u0008\u000B\u000C\u000E-\u001F]", "");

        // Find the first balanced [...] in the string.
        var arraySlice = ExtractBalancedObject(sanitized, '[', ']');
        if (arraySlice is null) return new List<AiGeneratedQuestionDto>();

        try
        {
            using var doc = JsonDocument.Parse(
                arraySlice,
                new JsonDocumentOptions { AllowTrailingCommas = true });
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return new List<AiGeneratedQuestionDto>();
            return ExtractQuestionsFromArrayElement(doc.RootElement);
        }
        catch (JsonException)
        {
            return ParseArrayStreaming(arraySlice);
        }
    }

    private static List<AiGeneratedQuestionDto> ParseArrayStreaming(string array)
    {
        return BatchParsingHelpers.ParseArrayStreaming(
            array,
            arr => ExtractQuestionsFromArrayElement(arr).AsEnumerable());
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

    private static List<AiGeneratedQuestionDto> ExtractQuestionsFromArrayElement(JsonElement array)
    {
        var result = new List<AiGeneratedQuestionDto>();
        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object) continue;
            if (!element.TryGetProperty("questionTitle", out var qtProp)) continue;
            if (qtProp.ValueKind != JsonValueKind.String) continue;

            var title = CleanText(qtProp.GetString() ?? "");
            if (string.IsNullOrWhiteSpace(title)) continue;

            int position = 0;
            if (element.TryGetProperty("position", out var posProp)
                && posProp.ValueKind == JsonValueKind.Number
                && posProp.TryGetInt32(out var p))
            {
                position = p;
            }

            if (!element.TryGetProperty("answers", out var ansProp)
                || ansProp.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var answers = new List<AiGeneratedAnswerDto>();
            var seenOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in ansProp.EnumerateArray())
            {
                if (a.ValueKind != JsonValueKind.Object) continue;
                if (!a.TryGetProperty("selectedOption", out var optProp)) continue;
                if (optProp.ValueKind != JsonValueKind.String) continue;

                var opt = CleanText(optProp.GetString() ?? "");
                if (string.IsNullOrWhiteSpace(opt)) continue;
                if (!seenOptions.Add(opt)) continue;

                var isCorrect = a.TryGetProperty("isCorrect", out var icProp)
                    && icProp.ValueKind == JsonValueKind.True;

                answers.Add(new AiGeneratedAnswerDto(opt, isCorrect));
            }

            // Filter out placeholder answers the model copied from the prompt
            answers.RemoveAll(a => IsPlaceholderText(a.SelectedOption));

            // Require exactly 4 answers for quality
            if (answers.Count < 4) continue;

            // Keep only the first 4 answers if model produced more
            if (answers.Count > 4)
                answers = answers.Take(4).ToList();

            var correctCount = answers.Count(x => x.IsCorrect);
            if (correctCount == 0)
            {
                // Shuffle so the correct answer lands in a random position (A/B/C/D)
                var rng = Random.Shared;
                var idx = rng.Next(answers.Count);
                answers[idx] = answers[idx] with { IsCorrect = true };
            }
            else if (correctCount > 1)
            {
                var firstKept = false;
                for (var i = 0; i < answers.Count; i++)
                {
                    if (!answers[i].IsCorrect) continue;
                    if (firstKept) answers[i] = answers[i] with { IsCorrect = false };
                    else firstKept = true;
                }
            }

            // Randomize answer order so correct answer isn't always Option A
            if (answers.Count >= 2)
            {
                var rng = Random.Shared;
                for (var i = answers.Count - 1; i > 0; i--)
                {
                    var j = rng.Next(i + 1);
                    (answers[i], answers[j]) = (answers[j], answers[i]);
                }
            }

            result.Add(new AiGeneratedQuestionDto(
                title,
                QuestionType.SingleChoice,
                position,
                answers));
        }
        return result;
    }

    private static string? ExtractBalancedObject(string text, char open, char close)
    {
        return BatchGeneratorBase<object>.ExtractBalanced(text, open, close) is { } result
            ? result
            : null;
    }

    private static AiGeneratedQuestionDto? NormalizeQuestion(
        AiGeneratedQuestionDto raw,
        int expectedPosition)
    {
        var title = CleanText(raw.QuestionTitle);
        if (string.IsNullOrWhiteSpace(title)) return null;
        if (title.Length < 10) return null;

        // Reject placeholder titles the model copied from the prompt
        if (IsPlaceholderText(title)) return null;

        // Auto-append ? if missing — small models often forget it
        if (!title.TrimEnd().EndsWith("?"))
        {
            title = title.TrimEnd('.', '!', ' ') + "?";
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var answers = new List<AiGeneratedAnswerDto>();
        foreach (var a in raw.Answers ?? new List<AiGeneratedAnswerDto>())
        {
            var opt = CleanText(a.SelectedOption);
            if (string.IsNullOrWhiteSpace(opt)) continue;
            if (IsPlaceholderText(opt)) continue; // Skip placeholder answers
            if (!seen.Add(opt)) continue;
            answers.Add(new AiGeneratedAnswerDto(opt, a.IsCorrect));
        }

        // Accept 3+ answers (small models sometimes only produce 3)
        if (answers.Count < 3) return null;
        if (answers.Count > 4)
            answers = answers.Take(4).ToList();

        // SingleChoice invariant: exactly one correct answer.
        var correctCount = answers.Count(x => x.IsCorrect);
        if (correctCount == 0)
        {
            var rng = Random.Shared;
            var idx = rng.Next(answers.Count);
            answers[idx] = answers[idx] with { IsCorrect = true };
        }
        else if (correctCount > 1)
        {
            var firstKept = false;
            for (var i = 0; i < answers.Count; i++)
            {
                if (!answers[i].IsCorrect) continue;
                if (firstKept) answers[i] = answers[i] with { IsCorrect = false };
                else firstKept = true;
            }
        }

        return new AiGeneratedQuestionDto(
            title,
            QuestionType.SingleChoice,
            expectedPosition,
            answers);
    }

    /// <summary>
    /// Fixes mojibake (UTF-8 bytes misread as Latin-1) commonly found in PDF-extracted Vietnamese text.
    /// </summary>
    private static string FixMojibake(string input) => TextSanitizer.FixMojibake(input);

    private static string CleanText(string s) => TextSanitizer.CleanBracketedReferences(s);

    private async Task<Quiz> PersistQuizAsync(
        Guid documentId,
        string fallbackTitle,
        AiGeneratedQuizResponseDto result,
        CancellationToken cancellationToken)
    {
        var quiz = new Quiz
        {
            DocumentId = documentId,
            Title = string.IsNullOrWhiteSpace(result.QuizTitle) ? fallbackTitle : result.QuizTitle
        };
        await _unitOfWork.Quizzes.AddAsync(quiz, cancellationToken);

        foreach (var q in result.Questions)
        {
            var question = new Question
            {
                QuizId = quiz.Id,
                Title = q.QuestionTitle,
                Type = q.QuestionType,
                Position = q.Position
            };
            await _unitOfWork.Questions.AddAsync(question, cancellationToken);

            foreach (var a in q.Answers ?? new List<AiGeneratedAnswerDto>())
            {
                await _unitOfWork.Answers.AddAsync(new Answer
                {
                    QuestionId = question.Id,
                    SelectedOption = a.SelectedOption,
                    IsCorrect = a.IsCorrect
                }, cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);


        return quiz;
    }
}
