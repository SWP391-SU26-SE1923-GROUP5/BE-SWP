using AIStudyHub.Business.DTOs.Common;
using AIStudyHub.Business.DTOs.Gamification;
using AIStudyHub.Business.DTOs.Notifications;
using AIStudyHub.Data.Enums;

namespace AIStudyHub.Business.Interfaces.Services;

/// <summary>
/// Abstraction over the real-time notification channel (SignalR today, swappable for SSE later).
/// Implementations MUST be safe to call from any background service.
/// </summary>
public interface IRealTimeNotificationService
{
    Task SendNotificationAsync(RealTimeNotification notification, CancellationToken cancellationToken = default);
    Task NotifyDocumentProcessedAsync(Guid userId, Guid documentId, string title, CancellationToken cancellationToken = default);
    Task NotifyStreakAtRiskAsync(Guid userId, int currentStreak, int hoursRemaining, CancellationToken cancellationToken = default);
    Task NotifyNewFlashcardsReadyAsync(Guid userId, Guid documentId, string title, int count, CancellationToken cancellationToken = default);
    Task NotifyQuizReadyAsync(Guid userId, Guid quizId, string title, CancellationToken cancellationToken = default);
    Task NotifyLevelUpAsync(Guid userId, int newLevel, int totalXp, CancellationToken cancellationToken = default);

    /// <summary>
    /// Plan C4 / B.3.2 — broadcast when a user's paid tier is about to expire.
    /// </summary>
    Task NotifyTierExpiringSoonAsync(
        Guid userId,
        string tierName,
        DateTime expiresAt,
        int daysRemaining,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Plan C3: broadcast a badge unlock. Payload is the freshly-unlocked AchievementDto
    /// so the frontend can show a celebratory card with the same data it gets from
    /// <c>GET /api/Gamification/achievements</c>.
    /// </summary>
    Task NotifyBadgeEarnedAsync(Guid userId, AchievementDto achievement, CancellationToken cancellationToken = default);
}
