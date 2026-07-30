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

/// <summary>Plan C3 / B.2.3 — wraps the review + XP earned + any badges just unlocked by this turn.</summary>
public sealed record ReviewFlashcardResultDto(
    FlashcardReviewResponseDto Review,
    int XpEarned,
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

public sealed record FlashcardReviewHistoryItemDto(
    Guid AttemptId,
    Guid FlashcardId,
    Guid DocumentId,
    string DocumentTitle,
    string Front,
    ReviewQuality Quality,
    int? TimeSpentSeconds,
    int XpEarned,
    DateTime ReviewedAt);

public sealed record FlashcardReviewHistoryDetailDto(
    Guid AttemptId,
    Guid FlashcardId,
    Guid DocumentId,
    string DocumentTitle,
    Guid SubjectId,
    string SubjectCode,
    string SubjectName,
    string Front,
    string Back,
    ReviewQuality Quality,
    int? TimeSpentSeconds,
    float PreviousEaseFactor,
    float ResultEaseFactor,
    int PreviousInterval,
    int ResultInterval,
    int PreviousRepetitions,
    int ResultRepetitions,
    DateTime PreviousNextReviewDate,
    DateTime ResultNextReviewDate,
    int XpEarned,
    DateTime ReviewedAt);
