using AIStudyHub.Business.Enums;

namespace AIStudyHub.Business.Entities;

public sealed class Vote : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid DocumentId { get; set; }
    public VoteType Type { get; set; }

    public User User { get; set; } = null!;
    public Document Document { get; set; } = null!;
}
