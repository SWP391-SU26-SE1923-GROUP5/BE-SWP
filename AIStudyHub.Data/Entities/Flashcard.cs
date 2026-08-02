namespace AIStudyHub.Data.Entities;

public sealed class Flashcard : BaseEntity
{
    public Guid DeckId { get; set; }
    public string Front { get; set; } = string.Empty;
    public string Back { get; set; } = string.Empty;

    /// <summary>Number of times this card was answered incorrectly (quality &lt; 3).
    /// Used to identify "leech" cards for recommendation generation.</summary>
    public int Lapses { get; set; }

    public FlashcardDeck FlashcardDeck { get; set; } = null!;

    /// <summary>Convenience property to access the DocumentId through the FlashcardDeck.</summary>
    public Guid DocumentId => FlashcardDeck.DocumentId;
}
