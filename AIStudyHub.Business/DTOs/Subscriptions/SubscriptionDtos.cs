namespace AIStudyHub.Business.DTOs.Subscriptions;

/// <summary>
/// Current tier summary (Plan C4 / B.4.4). Includes <c>DaysRemaining</c> which is
/// negative after expiry so the frontend can render the grace-period banner.
/// </summary>
public sealed record CurrentSubscriptionDto(
    Guid TierId,
    string TierName,
    DateTime? StartedAt,
    DateTime? ExpiresAt,
    int? DaysRemaining,
    bool IsExpiringSoon,
    bool IsExpired);

/// <summary>One row in the user's subscription history (paid payment events).</summary>
public sealed record SubscriptionHistoryItemDto(
    Guid PaymentId,
    Guid TierId,
    string TierName,
    decimal Amount,
    DateTime PaymentDate,
    DateTime? ExpiresAt);

/// <summary>Plan C4 — what <c>GET /api/Subscriptions/me</c> returns.</summary>
public sealed record MySubscriptionDto(
    CurrentSubscriptionDto Current,
    IReadOnlyList<SubscriptionHistoryItemDto> History);