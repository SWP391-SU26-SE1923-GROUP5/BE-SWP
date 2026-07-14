using AIStudyHub.Business.DTOs.Common;
using AIStudyHub.Business.DTOs.Gamification;
using AIStudyHub.Business.DTOs.Notifications;
using AIStudyHub.Business.Hubs;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Data.Enums;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AIStudyHub.Business.Services;

public sealed class RealTimeNotificationService : IRealTimeNotificationService
{
    private const string ReceiveNotificationMethod = "ReceiveNotification";

    private readonly IHubContext<NotificationsHub> _notificationsHubContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RealTimeNotificationService> _logger;

    public RealTimeNotificationService(
        IHubContext<NotificationsHub> notificationsHubContext,
        IServiceScopeFactory scopeFactory,
        ILogger<RealTimeNotificationService> logger)
    {
        _notificationsHubContext = notificationsHubContext;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task SendNotificationAsync(RealTimeNotification notification, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<AIStudyHub.Data.Interfaces.IUnitOfWork>();
            var entity = new AIStudyHub.Data.Entities.Notification
            {
                Id = Guid.NewGuid(),
                UserId = notification.UserId,
                Title = notification.Title,
                Message = notification.Body,
                Type = notification.Type,
                PayloadJson = notification.Payload != null
                    ? System.Text.Json.JsonSerializer.Serialize(notification.Payload)
                    : null,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };
            await unitOfWork.Notifications.AddAsync(entity, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception dbEx)
        {
            _logger.LogWarning(dbEx, "Failed to save notification to database for user {UserId}", notification.UserId);
        }

        try
        {
            await _notificationsHubContext.Clients
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

    public Task NotifyQuizGradedAsync(
        Guid userId,
        Guid quizId,
        string quizTitle,
        int score,
        int maxScore,
        CancellationToken cancellationToken = default)
        => SendNotificationAsync(new RealTimeNotification(
            userId,
            "Quiz graded",
            $"You scored {score}/{maxScore} on \"{quizTitle}\".",
            NotificationType.QuizGraded,
            DateTime.UtcNow,
            new QuizGradedPayload(quizId, quizTitle, score, maxScore)), cancellationToken);

    public Task NotifyVoteReceivedAsync(
        Guid documentOwnerId,
        Guid voterId,
        Guid documentId,
        string documentTitle,
        VoteType voteType,
        CancellationToken cancellationToken = default)
        => SendNotificationAsync(new RealTimeNotification(
            documentOwnerId,
            "Vote received",
            $"Someone {(voteType == VoteType.Upvote ? "upvoted" : "downvoted")} your document \"{documentTitle}\".",
            NotificationType.VoteReceived,
            DateTime.UtcNow,
            new VoteReceivedPayload(documentId, documentTitle, voteType)), cancellationToken);

    public Task NotifyPaymentSucceededAsync(
        Guid userId,
        string tierName,
        DateTime activatedAt,
        DateTime expiresAt,
        CancellationToken cancellationToken = default)
        => SendNotificationAsync(new RealTimeNotification(
            userId,
            "Payment successful",
            $"Your {tierName} plan is now active until {expiresAt:MMM dd, yyyy}.",
            NotificationType.PaymentSucceeded,
            DateTime.UtcNow,
            new PaymentSucceededPayload(tierName, activatedAt, expiresAt)), cancellationToken);

    public Task NotifyDocumentFailedAsync(
        Guid userId,
        Guid documentId,
        string title,
        string errorMessage,
        CancellationToken cancellationToken = default)
        => SendNotificationAsync(new RealTimeNotification(
            userId,
            "Document processing failed",
            $"Failed to process \"{title}\": {errorMessage}",
            NotificationType.Document,
            DateTime.UtcNow,
            new DocumentFailedPayload(documentId, title, errorMessage)), cancellationToken);
}
