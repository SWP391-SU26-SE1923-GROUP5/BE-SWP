namespace AIStudyHub.Data.Entities;

public sealed class FlashcardDeck : BaseEntity
{
    public Guid DocumentId { get; set; }
    public string Name { get; set; } = string.Empty;

    public Document Document { get; set; } = null!;
    public ICollection<Flashcard> Flashcards { get; set; } = new List<Flashcard>();
}
