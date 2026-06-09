namespace AIStudyHub.Business.DTOs.Questions;

public sealed record QuestionResponseDto(Guid Id, Guid QuizId, string Title, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record CreateQuestionRequestDto(Guid QuizId, string Title);

public sealed record UpdateQuestionRequestDto(string Title);
