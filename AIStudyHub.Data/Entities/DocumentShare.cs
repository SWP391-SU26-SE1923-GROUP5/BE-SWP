using AIStudyHub.Data.Enums;

namespace AIStudyHub.Data.Entities;

public sealed class DocumentShare : BaseEntity
{
    public Guid DocumentId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Read or Edit. Defaults to Read if not specified.</summary>
    public ShareLevel Level { get; set; } = ShareLevel.Read;

    public Guid SharedBy { get; set; }
    public DateTime SharedAt { get; set; }

    public Document Document { get; set; } = null!;
    public User User { get; set; } = null!;
}
