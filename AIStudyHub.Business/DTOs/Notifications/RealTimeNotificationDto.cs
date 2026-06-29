using AIStudyHub.Data.Enums;

namespace AIStudyHub.Business.DTOs.Notifications;

public sealed record RealTimeNotification(
    Guid UserId,
    string Title,
    string Body,
    NotificationType Type,
    DateTime Timestamp,
    object? Payload = null);

public sealed record FlashcardsReadyPayload(Guid DocumentId, string Title, int Count);
public sealed record DocumentProcessedPayload(Guid DocumentId, string Title);
public sealed record StreakAtRiskPayload(int CurrentStreak, int HoursRemaining);
public sealed record QuizReadyPayload(Guid QuizId, string Title);
public sealed record LevelUpPayload(int NewLevel, int TotalXp);
public sealed record TierExpiringSoonPayload(string TierName, DateTime ExpiresAt, int DaysRemaining);
