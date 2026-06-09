namespace AIStudyHub.Business.DTOs.QuizSubmissions;

public sealed record QuizSubmissionResponseDto(Guid Id, Guid UserId, Guid QuizId, string Answers, decimal? Score, DateTime SubmittedAt, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record CreateQuizSubmissionRequestDto(Guid UserId, Guid QuizId, string Answers, decimal? Score);

public sealed record UpdateQuizSubmissionRequestDto(string Answers, decimal? Score);
