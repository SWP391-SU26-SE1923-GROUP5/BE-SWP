using AIStudyHub.Business.DTOs.Common;
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
}
