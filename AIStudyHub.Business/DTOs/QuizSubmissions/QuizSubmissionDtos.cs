using AIStudyHub.Business.DTOs.Gamification;
using AIStudyHub.Data.Enums;

namespace AIStudyHub.Business.DTOs.QuizSubmissions;

public sealed record QuizSubmissionResponseDto(
    Guid Id,
    Guid UserId,
    Guid QuizId,
    string QuizTitle,
    string DocumentTitle,
    string SubjectCode,
    int Score,
    int MaxScore,
    int TotalCorrect,
    int? DurationSeconds,
    double PercentageScore,
    DateTime? GradedAt,
    DateTime SubmittedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record QuizSubmissionOptionDetailDto(
    Guid AnswerId,
    string Text,
    bool IsSelected,
    bool IsCorrect);

public sealed record QuizSubmissionQuestionDetailDto(
    Guid QuestionId,
    string Title,
    QuestionType Type,
    int Position,
    IReadOnlyList<QuizSubmissionOptionDetailDto> Options);

public sealed record QuizSubmissionDetailDto(
    Guid Id,
    Guid QuizId,
    string QuizTitle,
    Guid DocumentId,
    string DocumentTitle,
    Guid SubjectId,
    string SubjectCode,
    string SubjectName,
    int Score,
    int MaxScore,
    int TotalCorrect,
    int? DurationSeconds,
    double PercentageScore,
    DateTime? GradedAt,
    DateTime SubmittedAt,
    IReadOnlyList<QuizSubmissionQuestionDetailDto> Questions);

/// <summary>
/// Enriched history item with quiz/document metadata. Returned by GetMyHistoryAsync.
/// </summary>
public sealed record QuizSubmissionHistoryDto(
    Guid Id,
    Guid UserId,
    Guid QuizId,
    string QuizTitle,
    string DocumentTitle,
    string SubjectCode,
    int Score,
    int MaxScore,
    int TotalCorrect,
    int? DurationSeconds,
    double PercentageScore,
    DateTime? GradedAt,
    DateTime SubmittedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    public static QuizSubmissionHistoryDto FromResponse(QuizSubmissionResponseDto r) =>
        new(r.Id, r.UserId, r.QuizId, r.QuizTitle, r.DocumentTitle, r.SubjectCode,
            r.Score, r.MaxScore, r.TotalCorrect, r.DurationSeconds, r.PercentageScore,
            r.GradedAt, r.SubmittedAt, r.CreatedAt, r.UpdatedAt);
}

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
