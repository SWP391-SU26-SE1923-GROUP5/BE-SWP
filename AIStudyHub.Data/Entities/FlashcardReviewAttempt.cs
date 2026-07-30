using AIStudyHub.Data.Enums;

namespace AIStudyHub.Data.Entities;

public sealed class FlashcardReviewAttempt : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid FlashcardId { get; set; }
    public ReviewQuality Quality { get; set; }
    public int? TimeSpentSeconds { get; set; }
    public float PreviousEaseFactor { get; set; }
    public float ResultEaseFactor { get; set; }
    public int PreviousInterval { get; set; }
    public int ResultInterval { get; set; }
    public int PreviousRepetitions { get; set; }
    public int ResultRepetitions { get; set; }
    public DateTime PreviousNextReviewDate { get; set; }
    public DateTime ResultNextReviewDate { get; set; }
    public int XpEarned { get; set; }

    public User User { get; set; } = null!;
    public Flashcard Flashcard { get; set; } = null!;
}
