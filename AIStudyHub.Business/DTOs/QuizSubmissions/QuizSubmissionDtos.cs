using AIStudyHub.Business.DTOs.Gamification;

namespace AIStudyHub.Business.DTOs.QuizSubmissions;

public sealed record QuizSubmissionResponseDto(Guid Id, Guid UserId, Guid QuizId, string Answers, int Score, int MaxScore, int TotalCorrect, DateTime? GradedAt, DateTime SubmittedAt, DateTime CreatedAt, DateTime? UpdatedAt);

/// <summary>
/// Plan C3 / B.2.2: return value of <c>QuizSubmissionService.SubmitAsync</c>.
/// Wraps the submission + XP earned + a list of badges the user JUST unlocked so the
/// frontend can celebrate without an extra round-trip to <c>/api/Gamification/achievements</c>.
/// </summary>
public sealed record SubmitQuizResultDto(
    QuizSubmissionResponseDto Submission,
    int XpEarned,
    IReadOnlyList<AchievementDto> NewAchievements);

/// <summary>
/// Time spent on the quiz is optional (Plan C2, Master Spec B.2.2). Submitting a
/// positive value routes it into UserStats.TotalStudySeconds + StudyLog.TimeSpentSeconds
/// so the dashboard analytics can render cumulative hours.
/// </summary>
public sealed record CreateQuizSubmissionRequestDto(
    Guid UserId,
    Guid QuizId,
    string Answers,
    int? DurationSeconds = null);

public sealed record UpdateQuizSubmissionRequestDto(string Answers);
