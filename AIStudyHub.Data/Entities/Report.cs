using AIStudyHub.Data.Enums;

namespace AIStudyHub.Data.Entities;

public sealed class Report : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid DocumentId { get; set; }
    public ReportCategory Category { get; set; }
    public string? Reason { get; set; }
    public ReportStatus Status { get; set; } = ReportStatus.Pending;
    public Guid? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public User User { get; set; } = null!;
    public Document Document { get; set; } = null!;
    public User? ResolvedByUser { get; set; }
}
