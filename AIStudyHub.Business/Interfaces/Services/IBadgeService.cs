using AIStudyHub.Business.DTOs.Common;
using AIStudyHub.Business.DTOs.Gamification;
using AIStudyHub.Data.Entities;

namespace AIStudyHub.Business.Interfaces.Services;

/// <summary>
/// Plan C3 — badge unlock engine. Each <c>Evaluate*</c> method is invoked from
/// the corresponding service (QuizSubmission, FlashcardReview, DocumentProcessing,
/// Gamification) and is **idempotent**: re-running it never awards XP twice because
/// the (UserId, BadgeId) unique index on UserBadge makes the insert a no-op.
/// </summary>
public interface IBadgeService
{
    /// <summary>
    /// Run after a quiz is graded. Checks SHARPSHOOTER (100%, ≥10q) and
    /// MASTERY_MATH (Math subject, ≥85%).
    /// </summary>
    Task<IReadOnlyList<AchievementDto>> EvaluateQuizBadgeAsync(
        Guid userId,
        QuizSubmission submission,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Run after a flashcard review. Checks CARDS_500 (500 unique cards reviewed).
    /// </summary>
    Task<IReadOnlyList<AchievementDto>> EvaluateFlashcardBadgeAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Run after a document finishes processing. Checks BOOKWORM (7 documents done).
    /// </summary>
    Task<IReadOnlyList<AchievementDto>> EvaluateDocumentBadgeAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Run after a streak is incremented by GamificationService. Checks STREAK_7D.
    /// </summary>
    Task<IReadOnlyList<AchievementDto>> EvaluateStreakBadgeAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Full catalogue of all badges + this user's progress / unlock state.
    /// Plan A.6 / <c>GET /api/Gamification/achievements</c>.
    /// </summary>
    Task<IReadOnlyList<AchievementDto>> GetAchievementsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
