namespace AIStudyHub.Data.Entities;

public sealed class Flashcard : BaseEntity
{
    public Guid DocumentId { get; set; }
    public string Front { get; set; } = string.Empty;
    public string Back { get; set; } = string.Empty;

    public Document Document { get; set; } = null!;
}
