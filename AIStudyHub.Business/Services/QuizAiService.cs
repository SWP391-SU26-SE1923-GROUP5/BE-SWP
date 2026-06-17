using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AIStudyHub.Business.DTOs.Quizzes;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Data.Entities;
using AIStudyHub.Business.DTOs.Quizzes;
using AIStudyHub.Business.DTOs.Rag;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Options;

using AIStudyHub.Data.Enums;
using AIStudyHub.Data.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.RegularExpressions;


namespace AIStudyHub.Business.Services;

public sealed class QuizAiService : IQuizAiService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRagChatService _ragChatService;
    private readonly ILogger<QuizAiService> _logger;

    public QuizAiService(
        IUnitOfWork unitOfWork,
        IRagChatService ragChatService,
        ILogger<QuizAiService> logger)
    {
        _unitOfWork = unitOfWork;
        _ragChatService = ragChatService;
        _logger = logger;
    }

    public async Task<AiGeneratedQuizResponseDto> GenerateAndPersistQuizAsync(
    private readonly ILocalAIService _localAIService;
    private readonly RagOptions _options;
    private readonly ILogger<QuizAiService> _logger;
    private static readonly Random _rng = new();

    public QuizAiService(
        IUnitOfWork unitOfWork,
        ILocalAIService localAIService,
        IOptions<RagOptions> options,
        ILogger<QuizAiService> logger)
    {
        _unitOfWork = unitOfWork;
        _localAIService = localAIService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AiGeneratedQuizResponseDto> GenerateQuizAsync(

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

        var document = await _unitOfWork.Documents.GetByIdAsync(documentId, cancellationToken)
            ?? throw new KeyNotFoundException("Document not found");


        var chunks = await _unitOfWork.DocumentChunks
            .Query()
            .Where(c => c.DocumentId == documentId)
            .OrderBy(c => c.OrderIndex)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var context = BuildContext(chunks);

        // llama3.2:1b can't reliably fill 10 question x 4 answer strings in
        // one shot. Chunk into small batches and retry underfilled batches.
        const int batchSize = 3;
        var allQuestions = new List<AiGeneratedQuestionDto>(request.numberOfQuestions);
        var seenTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var remaining = request.numberOfQuestions;
        var batchNumber = 0;
        var runningTitle = string.Empty;

        while (remaining > 0)
        {
            batchNumber++;
            var wantThisBatch = Math.Min(batchSize, remaining + 1); // +1 to absorb noise

            var prompt = BuildBatchPrompt(
                wantThisBatch,
                context,
                allQuestions,
                startingPosition: allQuestions.Count + 1);

            var batchQuestions = await RunBatchWithRetryAsync(
                prompt, wantThisBatch, batchNumber, cancellationToken);

            var added = 0;
            foreach (var q in batchQuestions)
            {
                if (allQuestions.Count >= request.numberOfQuestions)
                    break;

                var normalized = NormalizeQuestion(q, allQuestions.Count + 1);
                if (normalized is null) continue;

                if (!seenTitles.Add(normalized.QuestionTitle))
                    continue;

                allQuestions.Add(normalized);
                added++;
            }

            _logger.LogInformation(
                "Quiz batch {Batch}: wanted {Want}, parsed {Parsed}, accepted {Accepted}, total {Total}/{Requested}",
                batchNumber, wantThisBatch, batchQuestions.Count, added, allQuestions.Count, request.numberOfQuestions);

            if (added == 0)
                break;

            remaining = request.numberOfQuestions - allQuestions.Count;
        }

        if (allQuestions.Count == 0)
        {
            _logger.LogWarning(
                "No quiz questions generated for document {DocumentId}", documentId);
            return new AiGeneratedQuizResponseDto(
                $"Quiz on {document.Title}",
                new List<AiGeneratedQuestionDto>());
        }

        runningTitle = string.IsNullOrWhiteSpace(runningTitle)
            ? $"Quiz on {document.Title}"
            : runningTitle;

        var result = new AiGeneratedQuizResponseDto(runningTitle, allQuestions);

        await PersistQuizAsync(documentId, document.Title, result, cancellationToken);

        _logger.LogInformation(
            "Generated {Count}/{Requested} quiz questions for document {DocumentId}",
            allQuestions.Count, request.numberOfQuestions, documentId);

        return result;
    }

    private static string BuildContext(IReadOnlyList<Data.Entities.DocumentChunk> chunks)
    {
        var sb = new StringBuilder();
        foreach (var c in chunks)
        {
            if (string.IsNullOrWhiteSpace(c.ChunkJson)) continue;
            sb.AppendLine(c.ChunkJson);
            sb.AppendLine();
            if (sb.Length > 30_000) break;
        }
        return sb.ToString();
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
You are a JSON API. You generate multiple-choice quiz questions from a CONTEXT.

Return ONLY a valid JSON object. No markdown, no prose, no code fences, no commentary.

Schema (must match exactly):
{
  "quizTitle": "<short topic name>",
  "questions": [
    {
      "questionTitle": "<question text ending with ?>",
      "questionType": "SingleChoice",
      "position": <number>,
      "answers": [
        { "selectedOption": "<text>", "isCorrect": true },
        { "selectedOption": "<text>", "isCorrect": false },
        { "selectedOption": "<text>", "isCorrect": false },
        { "selectedOption": "<text>", "isCorrect": false }
      ]
    }
  ]
}

Strict requirements:
- Output EXACTLY {{count}} questions in the array.
- Each question MUST have EXACTLY 4 answers.
- EXACTLY ONE answer per question must have isCorrect = true; the other three must be false.
- "position" must start at {{startingPosition}} and increment by 1.
- "questionType" must be "SingleChoice" for every question.
- Every "selectedOption" string must be NON-EMPTY and DISTINCT within the same question.
- Every fact must come from CONTEXT.
- Each question must cover a DIFFERENT topic from the others.
- Output ONLY the JSON object. Start with '{' and end with '}'.

CONTEXT:
{{context}}{{avoidBlock}}
""";
    }

    private async Task<List<AiGeneratedQuestionDto>> RunBatchWithRetryAsync(
        string prompt,
        int wantThisBatch,
        int batchNumber,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 2;
        var best = new List<AiGeneratedQuestionDto>();

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string aiText;
            try
            {
                aiText = await _ragChatService.SendRawPromptAsync(prompt, 0.2f);
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

            if (parsed.Count > best.Count)
                best = parsed;

            if (parsed.Count >= Math.Max(1, wantThisBatch / 2))
                return parsed;

            _logger.LogWarning(
                "Quiz batch {Batch} attempt {Attempt}: only {Got}/{Want} questions, retrying",
                batchNumber, attempt, parsed.Count, wantThisBatch);
        }

        return best;
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
        var sanitized = Regex.Replace(
            array, @"[\u0000-\u0008\u000B\u000C\u000E-\u001F]", "");

        var result = new List<AiGeneratedQuestionDto>();
        var i = 0;
        while (i < sanitized.Length)
        {
            while (i < sanitized.Length && (char.IsWhiteSpace(sanitized[i]) || sanitized[i] == ','))
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
                using var doc = JsonDocument.Parse(
                    slice,
                    new JsonDocumentOptions { AllowTrailingCommas = true });

                result.AddRange(ExtractQuestionsFromArrayElement(
                    WrapSingleObject(doc.RootElement.Clone())));
            }
            catch (JsonException)
            {
                // Skip broken element.
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

            // The 1B model often emits 4 options but forgets to set isCorrect,
            // or marks multiple. We accept the question as long as we can
            // produce a SingleChoice answer. The "correct" flag is best-effort.
            if (answers.Count < 2) continue;

            var correctCount = answers.Count(x => x.IsCorrect);
            if (correctCount == 0)
            {
                // No answer marked correct: pick the first option. The user can
                // edit the answer in the UI.
                answers[0] = answers[0] with { IsCorrect = true };
            }
            else if (correctCount > 1)
            {
                // Multiple marked correct: keep the first as correct, demote rest.
                var firstKept = false;
                for (var i = 0; i < answers.Count; i++)
                {
                    if (!answers[i].IsCorrect) continue;
                    if (firstKept) answers[i] = answers[i] with { IsCorrect = false };
                    else firstKept = true;
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

    private static AiGeneratedQuestionDto? NormalizeQuestion(
        AiGeneratedQuestionDto raw,
        int expectedPosition)
    {
        var title = CleanText(raw.QuestionTitle);
        if (string.IsNullOrWhiteSpace(title)) return null;

        // Just accept any non-trivial title. The model may phrase questions in
        // many ways (can X?, why does X?, list..., describe...) and the
        // frontend already shows whatever title we return.
        if (title.Length < 3) return null;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var answers = new List<AiGeneratedAnswerDto>();
        foreach (var a in raw.Answers ?? new List<AiGeneratedAnswerDto>())
        {
            var opt = CleanText(a.SelectedOption);
            if (string.IsNullOrWhiteSpace(opt)) continue;
            if (!seen.Add(opt)) continue;
            answers.Add(new AiGeneratedAnswerDto(opt, a.IsCorrect));
        }
        if (answers.Count < 2) return null;

        // SingleChoice invariant: exactly one correct answer.
        var correctCount = answers.Count(x => x.IsCorrect);
        if (correctCount == 0)
        {
            answers[0] = answers[0] with { IsCorrect = true };
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

    private static string CleanText(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        return Regex.Replace(s, @"\s*\[[^\]]+\]", "").Trim();
    }

    private async Task PersistQuizAsync(
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
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            foreach (var a in q.Answers ?? new List<AiGeneratedAnswerDto>())
            {
                await _unitOfWork.Answers.AddAsync(new Answer
                {
                    QuestionId = question.Id,
                    SelectedOption = a.SelectedOption,
                    IsCorrect = a.IsCorrect
                }, cancellationToken);
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
            .ToListAsync(cancellationToken);

        var context = string.Join("\n\n",
            chunks.Select(c => ExtractChunkContent(c.ChunkJson)));

        _logger.LogDebug("Quiz context length={Length} for document {DocumentId}", context.Length, documentId);

        var questions = new List<AiGeneratedQuestionDto>();
        var seenTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenCorrectAnswers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var recentRejections = new List<string>(3);
        var maxRetriesPerQuestion = 3;
        var maxConsecutiveFailures = 5;
        var consecutiveFailures = 0;

        for (int i = 0; i < request.numberOfQuestions; i++)
        {
            var question = await TryGenerateSingleQuestionAsync(
                context,
                questions,
                i + 1,
                request.numberOfQuestions,
                seenTitles,
                seenCorrectAnswers,
                recentRejections,
                maxRetriesPerQuestion,
                cancellationToken);

            if (question is null)
            {
                consecutiveFailures++;
                if (consecutiveFailures >= maxConsecutiveFailures)
                {
                    _logger.LogError(
                        "Aborting quiz generation after {Count} consecutive failures",
                        consecutiveFailures);
                    break;
                }
                continue;
            }

            consecutiveFailures = 0;
            questions.Add(question);
        }

        if (questions.Count == 0)
            throw new InvalidOperationException("AI could not generate any questions.");

        _logger.LogInformation(
            "Generated {Count}/{Requested} questions for document {DocumentId}",
            questions.Count, request.numberOfQuestions, documentId);

        return new AiGeneratedQuizResponseDto(
            $"Quiz — {document.Title ?? "Generated"}",
            questions);
    }

    private async Task<AiGeneratedQuestionDto?> TryGenerateSingleQuestionAsync(
        string context,
        List<AiGeneratedQuestionDto> alreadyGenerated,
        int currentIndex,
        int totalRequested,
        HashSet<string> seenTitles,
        HashSet<string> seenCorrectAnswers,
        List<string> recentRejections,
        int maxRetries,
        CancellationToken cancellationToken)
    {
        var questionTypes = new[] { QuestionType.SingleChoice, QuestionType.MultipleChoice, QuestionType.TrueFalse };
        var pickedType = questionTypes[_rng.Next(questionTypes.Length)];
        var (typeName, expectedAnswers) = pickedType switch
        {
            QuestionType.SingleChoice   => ("SingleChoice",   4),
            QuestionType.MultipleChoice => ("MultipleChoice", 4),
            QuestionType.TrueFalse      => ("TrueFalse",      2),
            _                          => ("SingleChoice",   4)
        };

        var existingQuestions = alreadyGenerated.Count == 0
            ? "None"
            : string.Join("\n", alreadyGenerated.Select(q => $"- {q.QuestionTitle}"));

        string previousRejection;
        if (recentRejections.Count == 0)
        {
            previousRejection = "";
        }
        else
        {
            var rejectedList = string.Join("\n", recentRejections.Select(r => $"- \"{r}\""));
            previousRejection =
                $"\nATTENTION — your previous attempts for this question were rejected. " +
                $"You must NOT produce a title or correct answer that matches or paraphrases any of these:\n\n" +
                $"{rejectedList}\n\n" +
                $"Pick a clearly different question. " +
                $"If the CONTEXT does not contain enough distinct concepts, " +
                $"output ONLY the word: SKIP\n\n";
        }

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                var prompt = BuildPrompt(
                    context,
                    existingQuestions,
                    previousRejection,
                    typeName,
                    currentIndex,
                    totalRequested);

                var chatHistory = BuildChatHistory(alreadyGenerated);
                var aiText = await _localAIService.SendChatAsync(
                    systemPrompt: prompt,
                    history: chatHistory,
                    userMessage: $"Generate question {currentIndex} of {totalRequested}. Return ONLY the JSON object.",
                    cancellationToken: cancellationToken);

                _logger.LogDebug(
                    "AI raw response length={Length} (question {Index}/{Total}, attempt {Attempt})",
                    aiText.Length, currentIndex, totalRequested, attempt + 1);
                _logger.LogTrace("AI raw response:\n{Raw}", aiText);

                var result = ParseQuestionResponse(aiText, currentIndex, typeName, pickedType);
                if (result is null)
                {
                    _logger.LogWarning(
                        "Could not parse AI response as valid question (question {Index}, attempt {Attempt})",
                        currentIndex, attempt + 1);
                    continue;
                }

                var (rawTitle, correctAnswer) = result.Value;

                if (rawTitle == "SKIP")
                {
                    _logger.LogInformation(
                        "AI signaled skip for question {Index}: no more distinct topics",
                        currentIndex);
                    return new AiGeneratedQuestionDto(
                        $"__SKIP__:{currentIndex}",
                        QuestionType.SingleChoice,
                        currentIndex,
                        Array.Empty<AiGeneratedAnswerDto>());
                }

                rawTitle = Regex.Replace(rawTitle, @"\s*\[\d+\]", "").Trim();

                if (string.IsNullOrWhiteSpace(rawTitle))
                {
                    _logger.LogWarning(
                        "AI title became empty after cleanup (question {Index}, attempt {Attempt})",
                        currentIndex, attempt + 1);
                    continue;
                }

                correctAnswer = Regex.Replace(correctAnswer, @"\s*\[\d+\]", "").Trim();

                if (string.IsNullOrWhiteSpace(correctAnswer))
                {
                    _logger.LogWarning(
                        "AI correctAnswer became empty after cleanup (question {Index}, attempt {Attempt})",
                        currentIndex, attempt + 1);
                    continue;
                }

                if (!seenTitles.Add(rawTitle))
                {
                    _logger.LogWarning(
                        "AI returned duplicate title '{Title}' (question {Index}, attempt {Attempt})",
                        rawTitle, currentIndex, attempt + 1);
                    if (!recentRejections.Contains(rawTitle))
                        recentRejections.Add(rawTitle);
                    continue;
                }

                if (!seenCorrectAnswers.Add(correctAnswer))
                {
                    _logger.LogWarning(
                        "AI returned duplicate correctAnswer '{Answer}' (question {Index}, attempt {Attempt})",
                        correctAnswer, currentIndex, attempt + 1);
                    seenTitles.Remove(rawTitle);
                    if (!recentRejections.Contains(rawTitle))
                        recentRejections.Add(rawTitle);
                    continue;
                }

                var answers = BuildAnswerOptions(pickedType, correctAnswer, seenCorrectAnswers);

                _logger.LogDebug(
                    "Generated question {Index}/{Total}: {Title} ({Type}) with {Count} answers",
                    currentIndex, totalRequested, rawTitle, typeName, answers.Count);
                return new AiGeneratedQuestionDto(rawTitle, pickedType, currentIndex, answers);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(
                    ex,
                    "JSON parse error (question {Index}, attempt {Attempt})",
                    currentIndex, attempt + 1);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "AI call failed (question {Index}, attempt {Attempt})",
                    currentIndex, attempt + 1);
            }
        }

        _logger.LogError(
            "Could not generate question {Index}/{Total} after {Max} attempts",
            currentIndex, totalRequested, maxRetries);
        return null;
    }

    private static string BuildPrompt(
        string context,
        string existingQuestions,
        string previousRejection,
        string typeName,
        int currentIndex,
        int totalRequested)
    {
        return
$@"You are a JSON API. Return ONLY valid JSON. No markdown, no code fences.

Generate EXACTLY ONE quiz question as a single JSON object.

Use ONLY information from the CONTEXT section below.

Already generated question titles (do NOT repeat or paraphrase any):
{existingQuestions}
{previousRejection}
JSON format:
{{ ""questionTitle"": ""string"", ""correctAnswer"": ""string"" }}

Rules:
- Output ONLY a single JSON object. No explanation, no text outside JSON.
- To skip: output the single word SKIP (no braces, no JSON).
- ""questionTitle"" must be a non-empty question about the CONTEXT.
- ""correctAnswer"" must be the ONE correct answer to that question (1-2 sentences max).
- Do NOT repeat or paraphrase any title above.

This is question {currentIndex} of {totalRequested}. Generate a {typeName} question.

CONTEXT:
{context}";
    }

    private static (string title, string correctAnswer)? ParseQuestionResponse(
        string aiText,
        int currentIndex,
        string typeName,
        QuestionType pickedType)
    {
        var text = aiText.Trim();

        if (text.Equals("SKIP", StringComparison.OrdinalIgnoreCase))
            return ("SKIP", "");

        var stripped = Regex.Replace(text, @"```json\s*", "", RegexOptions.IgnoreCase);
        stripped = Regex.Replace(stripped, @"```\s*$", "", RegexOptions.IgnoreCase);
        stripped = stripped.Trim();

        var startIdx = stripped.IndexOf('{');
        var endIdx = stripped.LastIndexOf('}');
        if (startIdx < 0 || endIdx < startIdx)
        {
            var colonIdx = text.IndexOf(':');
            if (colonIdx < 0)
                return null;
            startIdx = text.LastIndexOf('{', colonIdx);
            endIdx = text.LastIndexOf('}', colonIdx);
            if (startIdx < 0 || endIdx < startIdx)
                return null;
            stripped = text.Substring(startIdx, endIdx - startIdx + 1);
        }
        else
        {
            stripped = stripped.Substring(startIdx, endIdx - startIdx + 1);
        }

        stripped = Regex.Replace(stripped, @"[\u0000-\u0008\u000B\u000C\u000E-\u001F]", "");

        using var doc = JsonDocument.Parse(stripped);
        var root = doc.RootElement;

        if (!root.TryGetProperty("questionTitle", out var titleProp)
            || titleProp.ValueKind != JsonValueKind.String)
            return null;

        var title = titleProp.GetString() ?? "";

        string correctAnswer = "";
        if (root.TryGetProperty("correctAnswer", out var caProp)
            && caProp.ValueKind == JsonValueKind.String)
        {
            correctAnswer = caProp.GetString() ?? "";
        }
        else if (root.TryGetProperty("answer", out var aProp)
            && aProp.ValueKind == JsonValueKind.String)
        {
            correctAnswer = aProp.GetString() ?? "";
        }
        else if (root.TryGetProperty("correct_option", out var coProp)
            && coProp.ValueKind == JsonValueKind.String)
        {
            correctAnswer = coProp.GetString() ?? "";
        }

        if (string.IsNullOrWhiteSpace(correctAnswer))
        {
            var answersArray = root.TryGetProperty("answers", out var arrProp)
                && arrProp.ValueKind == JsonValueKind.Array
                ? arrProp.EnumerateArray().ToList()
                : new List<JsonElement>();

            var correctItem = answersArray.FirstOrDefault(a =>
                a.TryGetProperty("isCorrect", out var ic)
                && ic.ValueKind == JsonValueKind.True);

            if (correctItem.ValueKind == JsonValueKind.Object
                && correctItem.TryGetProperty("selectedOption", out var so)
                && so.ValueKind == JsonValueKind.String)
            {
                correctAnswer = so.GetString() ?? "";
            }
        }

        return (title, correctAnswer);
    }

    private static List<AiGeneratedAnswerDto> BuildAnswerOptions(
        QuestionType type,
        string correctAnswer,
        HashSet<string> seenCorrectAnswers)
    {
        var answers = new List<AiGeneratedAnswerDto> { new(correctAnswer, true) };
        var decoyCount = type == QuestionType.TrueFalse ? 1 : 3;

        if (type == QuestionType.TrueFalse)
        {
            var wrong = correctAnswer.Equals("True", StringComparison.OrdinalIgnoreCase) ? "False" : "True";
            answers.Add(new AiGeneratedAnswerDto(wrong, false));
        }
        else
        {
            var distractors = new List<string>();
            foreach (var seen in seenCorrectAnswers)
            {
                if (!seen.Equals(correctAnswer, StringComparison.OrdinalIgnoreCase))
                    distractors.Add(seen);
            }
            ShuffleList(distractors, _rng);
            foreach (var d in distractors.Take(decoyCount))
                answers.Add(new AiGeneratedAnswerDto(d, false));

            while (answers.Count < 4)
            {
                var fallback = type == QuestionType.MultipleChoice
                    ? GetGenericFalseAnswer()
                    : GetGenericFalseAnswer();
                if (!answers.Any(a => a.SelectedOption.Equals(fallback, StringComparison.OrdinalIgnoreCase))
                    && !fallback.Equals(correctAnswer, StringComparison.OrdinalIgnoreCase))
                    answers.Add(new AiGeneratedAnswerDto(fallback, false));
            }
        }

        if (type == QuestionType.MultipleChoice)
        {
            var correctOnes = answers.Where(a => a.IsCorrect).ToList();
            if (correctOnes.Count == 1)
            {
                var extra = GetGenericFalseAnswer();
                if (!answers.Any(a => a.SelectedOption.Equals(extra, StringComparison.OrdinalIgnoreCase)))
                {
                    var idx = answers.FindIndex(a => !a.IsCorrect);
                    answers.Insert(idx >= 0 ? idx : 1, new AiGeneratedAnswerDto(extra, true));
                }
            }
        }

        ShuffleList(answers, _rng);
        return answers.Take(type == QuestionType.TrueFalse ? 2 : 4).ToList();
    }

    private static void ShuffleList<T>(List<T> list, Random rng)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private const int ChatHistoryLimit = 16;

    private static IReadOnlyList<ChatTurn> BuildChatHistory(List<AiGeneratedQuestionDto> alreadyGenerated)
    {
        if (alreadyGenerated.Count == 0)
            return Array.Empty<ChatTurn>();

        var turns = new List<ChatTurn>(alreadyGenerated.Count * 2);
        foreach (var q in alreadyGenerated.TakeLast(ChatHistoryLimit))
        {
            var correctAnswer = q.Answers.FirstOrDefault(a => a.IsCorrect)?.SelectedOption ?? "unknown";
            turns.Add(new ChatTurn(
                "user",
                $"Produce the next question. The previous question had title=\"{EscapeForJson(q.QuestionTitle)}\" and correctAnswer=\"{EscapeForJson(correctAnswer)}\"."));
            turns.Add(new ChatTurn(
                "assistant",
                $"{{ \"questionTitle\": \"{EscapeForJson(q.QuestionTitle)}\", \"correctAnswer\": \"{EscapeForJson(correctAnswer)}\" }}"));
        }
        return turns;
    }

    private static string EscapeForJson(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
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
        catch (JsonException) { }
        return chunkJson;
    }

    private static string GetGenericFalseAnswer()
    {
        var falseAnswers = new[]
        {
            "None of the other options",
            "This option is incorrect",
            "Another wrong answer",
            "A distractor option",
            "Not the correct choice",
            "An incorrect alternative",
            "This is not the right answer",
        };
        return falseAnswers[_rng.Next(falseAnswers.Length)];
    }
}
