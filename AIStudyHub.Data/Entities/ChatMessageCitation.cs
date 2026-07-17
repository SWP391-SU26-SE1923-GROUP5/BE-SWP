namespace AIStudyHub.Data.Entities;

public sealed class ChatMessageCitation : BaseEntity
{
    public Guid ChatMessageId { get; set; }
    public int CitationIndex { get; set; }
    public Guid DocumentId { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public int? PageNumber { get; set; }
    public double Relevance { get; set; }
    public string MatchType { get; set; } = string.Empty;
    public bool IsHighlightable { get; set; }
    public string? Reason { get; set; }

    public ChatMessage ChatMessage { get; set; } = null!;
}
