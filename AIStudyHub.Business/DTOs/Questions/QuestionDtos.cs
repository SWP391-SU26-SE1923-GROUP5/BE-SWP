using AIStudyHub.Data.Enums;
using AIStudyHub.Business.DTOs.Answers;

namespace AIStudyHub.Business.DTOs.Questions;

public sealed record QuestionResponseDto(
    Guid Id,
    Guid QuizId,
    string Title,
    QuestionType Type,
    int Position,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<AnswerResponseDto> Answers
);

public sealed record QuestionOptionDto(string Text);

public sealed record QuestionDto(
    string Title,
    IReadOnlyList<QuestionOptionDto> Options,
    IList<string>? DistractorExplanations);

public sealed record CreateQuestionRequestDto(Guid QuizId, string Title, QuestionType Type = QuestionType.SingleChoice, int Position = 0);

public sealed record UpdateQuestionRequestDto(string Title, QuestionType Type, int Position);
