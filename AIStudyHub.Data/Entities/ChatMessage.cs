namespace AIStudyHub.Data.Entities;

public sealed class ChatMessage : BaseEntity
{
    public Guid ChatSessionId { get; set; }
    public string Sender { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    public ChatSession ChatSession { get; set; } = null!;
}
