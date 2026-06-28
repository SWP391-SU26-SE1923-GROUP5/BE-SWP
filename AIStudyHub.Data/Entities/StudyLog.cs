using AIStudyHub.Data.Enums;

namespace AIStudyHub.Data.Entities;

/// <summary>
/// Append-only record of a learning activity. Powers subject mastery analytics,
/// daily charts, and AI recommendations. Created via GamificationService.
/// </summary>
public sealed class StudyLog : BaseEntity
{
    public Guid UserId { get; set; }
    public ActivityType ActivityType { get; set; }
    public Guid? DocumentId { get; set; }
    public string? SubjectCode { get; set; }
    public bool IsCorrect { get; set; }
    public int? TimeSpentSeconds { get; set; }
    public int XpEarned { get; set; }

    public User User { get; set; } = null!;
    public Document? Document { get; set; }
}
