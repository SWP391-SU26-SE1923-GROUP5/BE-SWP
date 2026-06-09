namespace AIStudyHub.Business.DTOs.Quizzes;

public sealed record QuizResponseDto(Guid Id, Guid DocumentId, string Title, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record CreateQuizRequestDto(Guid DocumentId, string Title);

public sealed record UpdateQuizRequestDto(string Title);
