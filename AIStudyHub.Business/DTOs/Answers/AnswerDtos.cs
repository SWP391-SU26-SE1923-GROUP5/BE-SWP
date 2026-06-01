namespace AIStudyHub.Business.DTOs.Answers;

public sealed record AnswerResponseDto(Guid Id, Guid QuestionId, string Text, bool IsCorrect, int SortOrder, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record CreateAnswerRequestDto(Guid QuestionId, string Text, bool IsCorrect, int SortOrder);

public sealed record UpdateAnswerRequestDto(string Text, bool IsCorrect, int SortOrder);
