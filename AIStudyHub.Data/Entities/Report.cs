using AIStudyHub.Data.Enums;

namespace AIStudyHub.Data.Entities;

public sealed class Report : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid DocumentId { get; set; }
    public string? Reason { get; set; }

    public User User { get; set; } = null!;
    public Document Document { get; set; } = null!;
}
