namespace AIStudyHub.Data.Entities;

/// <summary>
/// Per-user gamification state: XP, level, current/best streak.
/// Created on user registration, updated by GamificationService.
/// </summary>
public sealed class UserStats : BaseEntity
{
    public Guid UserId { get; set; }
    public int TotalXp { get; set; } = 0;
    public int CurrentLevel { get; set; } = 1;
    public int CurrentStreak { get; set; } = 0;
    public int BestStreak { get; set; } = 0;
    public DateTime? LastActivityDate { get; set; }

    public User User { get; set; } = null!;
}
