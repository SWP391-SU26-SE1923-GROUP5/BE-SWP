using AIStudyHub.Data.Enums;
using System.Text.Json.Serialization;

namespace AIStudyHub.Business.DTOs.Quizzes;
public sealed record CreateQuizRequestViaAiDto(
    [property: JsonRequired] int NumberOfQuestions);
public sealed record AiGeneratedAnswerDto(string SelectedOption, bool IsCorrect);

public sealed record AiGeneratedQuestionDto(
    string QuestionTitle,
    QuestionType QuestionType,
    int Position,
    IReadOnlyList<AiGeneratedAnswerDto> Answers
);

public sealed record AiGeneratedQuizResponseDto(string QuizTitle, IReadOnlyList<AiGeneratedQuestionDto> Questions);
