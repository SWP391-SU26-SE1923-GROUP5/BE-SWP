using AIStudyHub.Data.Enums;

namespace AIStudyHub.Data.Entities;

public sealed class Recommendation : BaseEntity
{
    public Guid UserId { get; set; }
    public RecommendationType Type { get; set; }
    public Guid? ReferenceId { get; set; } // SubjectId or FlashcardId
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public string Status { get; set; } = "Active"; // Active | Dismissed
    public DateTime? DismissedAt { get; set; }

    public User User { get; set; } = null!;
}
