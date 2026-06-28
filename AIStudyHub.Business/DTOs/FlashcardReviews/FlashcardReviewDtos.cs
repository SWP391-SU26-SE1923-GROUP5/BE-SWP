using AIStudyHub.Data.Enums;

namespace AIStudyHub.Business.DTOs.FlashcardReviews;

public sealed record ReviewFlashcardRequestDto(
    Guid FlashcardId,
    ReviewQuality Quality);

public sealed record FlashcardReviewResponseDto(
    Guid ReviewId,
    Guid FlashcardId,
    DateTime NextReviewDate,
    float EaseFactor,
    int Interval,
    int Repetitions);

public sealed record DueFlashcardDto(
    Guid ReviewId,
    Guid FlashcardId,
    Guid DocumentId,
    string Front,
    string Back,
    DateTime NextReviewDate);

public sealed record FlashcardReviewStatsDto(
    int TotalReviewed,
    int DueNow,
    int MasteredCount,
    float AverageEaseFactor);
