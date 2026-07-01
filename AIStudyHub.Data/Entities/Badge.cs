namespace AIStudyHub.Data.Entities;

/// <summary>
/// Catalog of achievement definitions. Seeded via ApplicationDbContext.SeedBadges.
/// Five pillars: Streak | Volume | Mastery | Accuracy | Content.
/// </summary>
public sealed class Badge : BaseEntity
{
    /// <summary>Stable machine-readable identifier (e.g. STREAK_7D, CARDS_500). Unique.</summary>
    public string Code { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>One of: Streak | Volume | Mastery | Accuracy | Content.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Threshold value that must be met for the badge to unlock.</summary>
    public decimal TargetValue { get; set; }

    public string IconUrl { get; set; } = string.Empty;

    /// <summary>Bonus XP awarded once on unlock. Idempotent (see UserBadge unique index).</summary>
    public int XpReward { get; set; }

    public ICollection<UserBadge> UserBadges { get; set; } = new List<UserBadge>();
}
