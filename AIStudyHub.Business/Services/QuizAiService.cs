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
        var document = await _unitOfWork.Documents.GetByIdAsync(documentId, cancellationToken)
            ?? throw new KeyNotFoundException("Document not found");

        var chunks = await _unitOfWork.DocumentChunks
            .Query()
            .Where(c => c.DocumentId == documentId)
            .OrderBy(c => c.OrderIndex)
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
