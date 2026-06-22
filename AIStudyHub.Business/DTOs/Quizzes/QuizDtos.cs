namespace AIStudyHub.Business.DTOs.Quizzes;

public sealed record QuizResponseDto(Guid Id, Guid DocumentId, string Title, DateTime CreatedAt, DateTime? UpdatedAt, System.Collections.Generic.IReadOnlyList<AIStudyHub.Business.DTOs.Questions.QuestionResponseDto>? Questions = null);

public sealed record CreateQuizRequestDto(Guid DocumentId, string Title);

public sealed record UpdateQuizRequestDto(string Title);

