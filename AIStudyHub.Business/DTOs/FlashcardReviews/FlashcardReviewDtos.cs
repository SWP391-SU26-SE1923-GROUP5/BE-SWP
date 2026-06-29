using AIStudyHub.Business.DTOs.Gamification;
using AIStudyHub.Data.Enums;

namespace AIStudyHub.Business.DTOs.FlashcardReviews;

/// <summary>
/// TimeSpentSeconds is optional (Plan C2, Master Spec B.2.3). When supplied, the
/// value is added to UserStats.TotalStudySeconds via IGamificationService.AwardXpAsync
/// so /api/Analytics/dashboard can show cumulative hours.
/// </summary>
public sealed record ReviewFlashcardRequestDto(
    Guid FlashcardId,
    ReviewQuality Quality,
    int? TimeSpentSeconds = null);

public sealed record FlashcardReviewResponseDto(
    Guid ReviewId,
    Guid FlashcardId,
    DateTime NextReviewDate,
    float EaseFactor,
    int Interval,
    int Repetitions);

/// <summary>Plan C3 / B.2.3 — wraps the review + any badges just unlocked by this turn.</summary>
public sealed record ReviewFlashcardResultDto(
    FlashcardReviewResponseDto Review,
    IReadOnlyList<AchievementDto> NewAchievements);

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
