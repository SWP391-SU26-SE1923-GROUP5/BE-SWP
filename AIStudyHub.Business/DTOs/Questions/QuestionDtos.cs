using AIStudyHub.Business.Enums;

namespace AIStudyHub.Business.DTOs.Questions;

public sealed record QuestionResponseDto(Guid Id, Guid QuizId, string Text, QuestionType Type, int SortOrder, decimal Points, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record CreateQuestionRequestDto(Guid QuizId, string Text, QuestionType Type, int SortOrder, decimal Points);

public sealed record UpdateQuestionRequestDto(string Text, QuestionType Type, int SortOrder, decimal Points);
