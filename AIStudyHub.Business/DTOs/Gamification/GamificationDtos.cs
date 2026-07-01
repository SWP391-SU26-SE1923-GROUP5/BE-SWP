using AIStudyHub.Data.Enums;

namespace AIStudyHub.Business.DTOs.Gamification;

/// <summary>
/// Time window for <see cref="LeaderboardEntryDto"/> aggregation.
/// Drives how <c>GET /api/Gamification/leaderboard</c> computes each user's score.
/// </summary>
public enum LeaderboardPeriod
{
    /// <summary>Cumulative XP from the very first activity. Default value (backward-compatible).</summary>
    AllTime = 0,

    /// <summary>Sum of XP earned in the last 7 rolling days, sourced from <c>StudyLog</c>.</summary>
    Weekly = 1,

    /// <summary>Sum of XP earned in the last 30 rolling days, sourced from <c>StudyLog</c>.</summary>
    Monthly = 2
}

public sealed record UserStatsResponseDto(
    int TotalXp,
    int CurrentLevel,
    int CurrentStreak,
    int BestStreak,
    DateTime? LastActivityDate,
    int XpToNextLevel);

/// <summary>
/// One row on the leaderboard. <see cref="TotalXp"/> is always the cumulative All-Time XP
/// (for client display / tooltip), <see cref="Xp"/> is the value the user is actually ranked
/// by within the requested <see cref="Period"/> (so it equals <see cref="TotalXp"/> for
/// <see cref="LeaderboardPeriod.AllTime"/>).
/// </summary>
public sealed record LeaderboardEntryDto(
    Guid UserId,
    string FullName,
    int TotalXp,
    int Xp,
    int CurrentLevel,
    int CurrentStreak,
    int Rank,
    LeaderboardPeriod Period);

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
