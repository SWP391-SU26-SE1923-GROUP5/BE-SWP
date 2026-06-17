namespace AIStudyHub.Business.DTOs.QuizSubmissions;

public sealed record QuizSubmissionResponseDto(Guid Id, Guid UserId, Guid QuizId, string Answers, int Score, int MaxScore, int TotalCorrect, DateTime? GradedAt, DateTime SubmittedAt, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record CreateQuizSubmissionRequestDto(Guid UserId, Guid QuizId, string Answers);

public sealed record UpdateQuizSubmissionRequestDto(string Answers);
