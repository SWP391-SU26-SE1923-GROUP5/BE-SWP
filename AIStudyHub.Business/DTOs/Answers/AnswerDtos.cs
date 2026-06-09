namespace AIStudyHub.Business.DTOs.Answers;

public sealed record AnswerResponseDto(Guid Id, Guid QuestionId, string SelectedOption, bool IsCorrect, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record CreateAnswerRequestDto(Guid QuestionId, string SelectedOption, bool IsCorrect);

public sealed record UpdateAnswerRequestDto(string SelectedOption, bool IsCorrect);
