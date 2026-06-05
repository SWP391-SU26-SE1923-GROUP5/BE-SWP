namespace AIStudyHub.Data.Entities;

public sealed class ChatSession : BaseEntity
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;

    public User User { get; set; } = null!;
    public ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
}
