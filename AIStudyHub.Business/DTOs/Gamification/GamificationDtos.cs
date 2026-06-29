using AIStudyHub.Data.Enums;

namespace AIStudyHub.Business.DTOs.Gamification;

public sealed record UserStatsResponseDto(
    int TotalXp,
    int CurrentLevel,
    int CurrentStreak,
    int BestStreak,
    DateTime? LastActivityDate,
    int XpToNextLevel);

public sealed record LeaderboardEntryDto(
    Guid UserId,
    string FullName,
    int TotalXp,
    int CurrentLevel,
    int CurrentStreak,
    int Rank);

public sealed record XpAwardRequest(
    Guid UserId,
    int XpEarned,
    bool IsCorrect,
    ActivityType ActivityType,
    Guid? DocumentId,
    string? SubjectCode,
    int? TimeSpentSeconds);

public sealed record XpAwardResult(
    int XpEarned,
    int TotalXp,
    int PreviousLevel,
    int NewLevel,
    bool LeveledUp,
    int CurrentStreak,
    int BestStreak,
    int TotalStudySeconds);

/// <summary>
/// One achievement (badge definition + a user's progress / unlock state).
/// Plan A.6 + B.2.1. Used by <c>GET /api/Gamification/achievements</c> and returned
/// inline inside quiz / flashcard responses so the client can celebrate unlocks.
/// </summary>
public sealed record AchievementDto(
    Guid Id,
    string Code,
    string Title,
    string Description,
    string Category,
    decimal TargetValue,
    string IconUrl,
    int XpReward,
    bool IsUnlocked,
    DateTime? EarnedDate,
    decimal CurrentProgress);
