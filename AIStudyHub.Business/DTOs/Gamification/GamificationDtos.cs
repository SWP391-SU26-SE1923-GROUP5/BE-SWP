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
    int BestStreak);
