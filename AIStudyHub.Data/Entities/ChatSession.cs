namespace AIStudyHub.Data.Entities;

public sealed class ChatSession : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? DocumentId { get; set; }
    public string SessionTitle { get; set; } = string.Empty;

    public User User { get; set; } = null!;
    public Document? Document { get; set; }
    public ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
}
