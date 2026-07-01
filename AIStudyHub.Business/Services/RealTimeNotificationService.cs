using AIStudyHub.Business.DTOs.Common;
using AIStudyHub.Business.DTOs.Gamification;
using AIStudyHub.Business.DTOs.Notifications;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Data.Enums;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace AIStudyHub.Business.Services;

/// <summary>
/// SignalR-backed implementation of <see cref="IRealTimeNotificationService"/>.
///
/// The concrete hub class lives in AIStudyHub.API (where transport endpoints belong).
/// We receive <c>IHubContext&lt;Hub&gt;</c> and broadcast to the user's group, which the
/// API hub manages via <c>JoinGroup(userId)</c>. This way the Business layer never has to
/// reference the concrete hub class.
/// </summary>
public sealed class RealTimeNotificationService : IRealTimeNotificationService
{
    private const string ReceiveNotificationMethod = "ReceiveNotification";

    private readonly IHubContext<Hub> _hubContext;
    private readonly ILogger<RealTimeNotificationService> _logger;

    public RealTimeNotificationService(
        IHubContext<Hub> hubContext,
        ILogger<RealTimeNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendNotificationAsync(RealTimeNotification notification, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients
                .Group(notification.UserId.ToString())
                .SendAsync(ReceiveNotificationMethod, notification, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RealTimeNotificationService failed to send notification to user {UserId}", notification.UserId);
        }
    }

    public Task NotifyDocumentProcessedAsync(Guid userId, Guid documentId, string title, CancellationToken cancellationToken = default)
        => SendNotificationAsync(new RealTimeNotification(
            userId,
            "Document processed",
            $"\"{title}\" is ready.",
            NotificationType.Document,
            DateTime.UtcNow,
            new DocumentProcessedPayload(documentId, title)), cancellationToken);

    public Task NotifyStreakAtRiskAsync(Guid userId, int currentStreak, int hoursRemaining, CancellationToken cancellationToken = default)
        => SendNotificationAsync(new RealTimeNotification(
            userId,
            "Streak at risk",
            $"Your {currentStreak}-day streak ends in {hoursRemaining}h. Review a card now.",
            NotificationType.System,
            DateTime.UtcNow,
            new StreakAtRiskPayload(currentStreak, hoursRemaining)), cancellationToken);

    public Task NotifyNewFlashcardsReadyAsync(Guid userId, Guid documentId, string title, int count, CancellationToken cancellationToken = default)
        => SendNotificationAsync(new RealTimeNotification(
            userId,
            "Flashcards ready",
            $"{count} new flashcard(s) ready for \"{title}\".",
            NotificationType.Quiz,
            DateTime.UtcNow,
            new FlashcardsReadyPayload(documentId, title, count)), cancellationToken);

    public Task NotifyQuizReadyAsync(Guid userId, Guid quizId, string title, CancellationToken cancellationToken = default)
        => SendNotificationAsync(new RealTimeNotification(
            userId,
            "Quiz ready",
            $"Quiz \"{title}\" is available.",
            NotificationType.Quiz,
            DateTime.UtcNow,
            new QuizReadyPayload(quizId, title)), cancellationToken);

    public Task NotifyLevelUpAsync(Guid userId, int newLevel, int totalXp, CancellationToken cancellationToken = default)
        => SendNotificationAsync(new RealTimeNotification(
            userId,
            "Level up!",
            $"You reached level {newLevel} with {totalXp} XP.",
            NotificationType.TierUpgraded,
            DateTime.UtcNow,
            new LevelUpPayload(newLevel, totalXp)), cancellationToken);

    public Task NotifyTierExpiringSoonAsync(
        Guid userId,
        string tierName,
        DateTime expiresAt,
        int daysRemaining,
        CancellationToken cancellationToken = default)
        => SendNotificationAsync(new RealTimeNotification(
            userId,
            "Tier expiring soon",
            $"Your {tierName} plan expires in {daysRemaining} day(s). Renew now to keep premium features.",
            NotificationType.TierExpired,
            DateTime.UtcNow,
            new TierExpiringSoonPayload(tierName, expiresAt, daysRemaining)), cancellationToken);

    public Task NotifyBadgeEarnedAsync(Guid userId, AchievementDto achievement, CancellationToken cancellationToken = default)
        => SendNotificationAsync(new RealTimeNotification(
            userId,
            $"Badge unlocked: {achievement.Title}",
            $"+{achievement.XpReward} XP — {achievement.Description}",
            NotificationType.Achievement,
            DateTime.UtcNow,
            achievement), cancellationToken);
}
