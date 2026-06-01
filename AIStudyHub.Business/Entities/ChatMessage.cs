namespace AIStudyHub.Business.Entities;

public sealed class ChatMessage : BaseEntity
{
    public Guid ChatSessionId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    public ChatSession ChatSession { get; set; } = null!;
}
