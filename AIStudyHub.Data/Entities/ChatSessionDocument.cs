namespace AIStudyHub.Data.Entities;

public sealed class ChatSessionDocument : BaseEntity
{
    public Guid ChatSessionId { get; set; }
    public Guid DocumentId { get; set; }

    public ChatSession ChatSession { get; set; } = null!;
    public Document Document { get; set; } = null!;
}
