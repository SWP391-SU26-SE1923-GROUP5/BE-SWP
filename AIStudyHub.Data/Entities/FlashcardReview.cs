namespace AIStudyHub.Data.Entities;

/// <summary>
/// Tracks a user's spaced-repetition progress for a single flashcard.
/// One row per (UserId, FlashcardId). Implements SM-2 algorithm fields.
/// </summary>
public sealed class FlashcardReview : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid FlashcardId { get; set; }

    /// <summary>SM-2 ease factor. Starts at 2.5, never goes below 1.3.</summary>
    public float EaseFactor { get; set; } = 2.5f;

    /// <summary>Number of days until the next review.</summary>
    public int Interval { get; set; } = 1;

    /// <summary>Count of consecutive successful reviews. Resets on failure.</summary>
    public int Repetitions { get; set; } = 0;

    /// <summary>When this card is next due for review (UTC).</summary>
    public DateTime NextReviewDate { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Flashcard Flashcard { get; set; } = null!;
}
