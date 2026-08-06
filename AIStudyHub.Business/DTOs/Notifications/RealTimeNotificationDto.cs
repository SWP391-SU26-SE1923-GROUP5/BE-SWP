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
public sealed record DocumentProcessedPayload(
    Guid DocumentId,
    string Title,
    string Status,
    bool IsChatReady,
    string Message,
    bool CanRetry);
public sealed record StreakAtRiskPayload(int CurrentStreak, int HoursRemaining);
public sealed record QuizReadyPayload(Guid QuizId, string Title);
public sealed record LevelUpPayload(int NewLevel, int TotalXp);
public sealed record TierExpiringSoonPayload(string TierName, DateTime ExpiresAt, int DaysRemaining);

public sealed record QuizGradedPayload(Guid QuizId, string QuizTitle, int Score, int MaxScore);
public sealed record VoteReceivedPayload(Guid DocumentId, string DocumentTitle, VoteType VoteType);
public sealed record PaymentSucceededPayload(string TierName, DateTime ActivatedAt, DateTime ExpiresAt);
public sealed record DocumentFailedPayload(
    Guid DocumentId,
    string Title,
    string Status,
    bool IsChatReady,
    string Message,
    bool CanRetry);
public sealed record ReportUpdatedPayload(Guid ReportId, Guid DocumentId, ReportStatus NewStatus);
public sealed record ReportRejectedPayload(IReadOnlyList<Guid> ReportIds, Guid DocumentId);
