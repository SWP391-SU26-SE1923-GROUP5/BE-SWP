using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AIStudyHub.Business.DTOs.Quizzes;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;
using AIStudyHub.Data.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
    }
}
