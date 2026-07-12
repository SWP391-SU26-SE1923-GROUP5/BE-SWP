namespace AIStudyHub.Data.Entities;

public sealed class ChatSession : BaseEntity
{
    public Guid UserId { get; set; }
    public string SessionTitle { get; set; } = string.Empty;

    public User User { get; set; } = null!;
    public ICollection<ChatSessionDocument> ChatSessionDocuments { get; set; } = new List<ChatSessionDocument>();
    public ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
}
