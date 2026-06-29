namespace AIStudyHub.Data.Entities;

/// <summary>
/// Join entity that records when a user has unlocked a Badge.
/// Unique constraint on (UserId, BadgeId) enforces idempotency.
/// </summary>
public sealed class UserBadge : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid BadgeId { get; set; }
    public DateTime EarnedDate { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Badge Badge { get; set; } = null!;
}
