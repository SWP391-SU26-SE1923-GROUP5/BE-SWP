using AIStudyHub.Business.DTOs.Common;
using AIStudyHub.Business.DTOs.Gamification;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AIStudyHub.Business.Services;

/// <summary>
/// XP / level / streak engine. All side effects are scoped per-request and idempotent at the
/// (user, day, activity) level so calling twice in one day won't double-award streak.
/// </summary>
public sealed class GamificationService : IGamificationService
{
    private const int XpPerFlashcardCorrect = 10;
    private const int XpPerFlashcardIncorrect = 2;
    private const int XpPerQuizCorrect = 15;
    private const int XpPerQuizIncorrect = 5;

    // Level thresholds: Level N requires threshold[N] XP. Index 0 = Level 1.
    private static readonly int[] LevelThresholds =
    {
        0,      // Level 1
        100,    // Level 2
        250,    // Level 3
        500,    // Level 4
        1000,   // Level 5
        1750,   // Level 6
        2750,   // Level 7
        4000,   // Level 8
        5500,   // Level 9
        7500,   // Level 10
        10000   // Level 11 (cap -> stay here)
    };

    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GamificationService> _logger;
    private readonly IBadgeService? _badgeService;

    public GamificationService(
        IUnitOfWork unitOfWork,
        ILogger<GamificationService> logger,
        IBadgeService? badgeService = null)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _badgeService = badgeService;
    }

    public async Task EnsureUserStatsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) return;

        var existing = await _unitOfWork.UserStats
            .Query()
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
        if (existing is not null) return;

        var stats = new UserStats
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TotalXp = 0,
            CurrentLevel = 1,
            CurrentStreak = 0,
            BestStreak = 0,
            LastActivityDate = null,
            CreatedAt = DateTime.UtcNow
        };
        await _unitOfWork.UserStats.AddAsync(stats, cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "EnsureUserStatsAsync failed for user {UserId}", userId);
        }
    }

    public async Task<ServiceResult<UserStatsResponseDto>> GetStatsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            return ServiceResult<UserStatsResponseDto>.Fail("User id is required.");

        await EnsureUserStatsAsync(userId, cancellationToken);

        var stats = await _unitOfWork.UserStats
            .Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        if (stats is null)
            return ServiceResult<UserStatsResponseDto>.Fail("Could not load user stats.");

        var xpToNext = XpToNextLevel(stats.CurrentLevel, stats.TotalXp);
        return ServiceResult<UserStatsResponseDto>.Ok(new UserStatsResponseDto(
            stats.TotalXp,
            stats.CurrentLevel,
            stats.CurrentStreak,
            stats.BestStreak,
            stats.LastActivityDate,
            xpToNext));
    }

    public async Task<ServiceResult<XpAwardResult>> AwardXpAsync(XpAwardRequest request, CancellationToken cancellationToken = default)
    {
        if (request.UserId == Guid.Empty)
            return ServiceResult<XpAwardResult>.Fail("User id is required.");

        await EnsureUserStatsAsync(request.UserId, cancellationToken);

        var stats = await _unitOfWork.UserStats
            .Query()
            .FirstOrDefaultAsync(s => s.UserId == request.UserId, cancellationToken);
        if (stats is null)
            return ServiceResult<XpAwardResult>.Fail("User stats missing.");

        var previousLevel = stats.CurrentLevel;
        var xpAwarded = ComputeXpForRequest(request);
        var today = DateTime.UtcNow.Date;

        if (stats.LastActivityDate.HasValue)
        {
            var lastDate = stats.LastActivityDate.Value.Date;
            var daysSince = (today - lastDate).TotalDays;
            if (daysSince <= 0)
            {
                // Same day - no streak change.
            }
            else if (daysSince <= 1)
            {
                stats.CurrentStreak += 1;
            }
            else
            {
                stats.CurrentStreak = 1;
            }
        }
        else
        {
            stats.CurrentStreak = 1;
        }

        stats.BestStreak = Math.Max(stats.BestStreak, stats.CurrentStreak);
        stats.TotalXp += xpAwarded;
        stats.CurrentLevel = ComputeLevel(stats.TotalXp);
        stats.LastActivityDate = DateTime.UtcNow;

        // Plan C2: accumulate TimeSpentSeconds into UserStats.TotalStudySeconds.
        // Clamp to non-negative to avoid corrupting the column if upstream sends garbage.
        if (request.TimeSpentSeconds is int seconds && seconds > 0)
        {
            stats.TotalStudySeconds += seconds;
        }

        _unitOfWork.UserStats.Update(stats);

        var studyLog = new StudyLog
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            ActivityType = request.ActivityType,
            DocumentId = request.DocumentId,
            SubjectCode = request.SubjectCode,
            IsCorrect = request.IsCorrect,
            TimeSpentSeconds = request.TimeSpentSeconds,
            XpEarned = xpAwarded,
            CreatedAt = DateTime.UtcNow
        };
        await _unitOfWork.StudyLogs.AddAsync(studyLog, cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "AwardXpAsync failed for user {UserId}", request.UserId);
            return ServiceResult<XpAwardResult>.Fail("Could not save XP award.");
        }

        // Plan C3: streak badge hook. Wrapped so a badge failure doesn't void the XP award.
        if (_badgeService is not null)
        {
            try
            {
                var unlocked = await _badgeService.EvaluateStreakBadgeAsync(request.UserId, cancellationToken);
                if (unlocked.Count > 0)
                {
                    _logger.LogInformation("Unlocked {Count} streak badge(s) for user {UserId}", unlocked.Count, request.UserId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Streak badge evaluation failed for user {UserId}", request.UserId);
            }
        }

        return ServiceResult<XpAwardResult>.Ok(new XpAwardResult(
            xpAwarded,
            stats.TotalXp,
            previousLevel,
            stats.CurrentLevel,
            stats.CurrentLevel > previousLevel,
            stats.CurrentStreak,
            stats.BestStreak,
            stats.TotalStudySeconds));
    }

    public async Task<ServiceResult<IReadOnlyList<LeaderboardEntryDto>>> GetLeaderboardAsync(int top, CancellationToken cancellationToken = default)
    {
        var limit = top <= 0 ? 20 : Math.Min(top, 100);

        var rows = await _unitOfWork.UserStats
            .Query()
            .Include(s => s.User)
            .OrderByDescending(s => s.TotalXp)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var leaderboard = rows
            .Where(s => s.User is not null)
            .Select((s, idx) => new LeaderboardEntryDto(
                s.UserId,
                s.User!.FullName ?? string.Empty,
                s.TotalXp,
                s.CurrentLevel,
                s.CurrentStreak,
                idx + 1))
            .ToList();

        return ServiceResult<IReadOnlyList<LeaderboardEntryDto>>.Ok(leaderboard);
    }

    private static int ComputeXpForRequest(XpAwardRequest req) =>
        req.ActivityType switch
        {
            Data.Enums.ActivityType.FlashcardReview => req.IsCorrect ? XpPerFlashcardCorrect : XpPerFlashcardIncorrect,
            Data.Enums.ActivityType.QuizSubmission => req.IsCorrect ? XpPerQuizCorrect : XpPerQuizIncorrect,
            _ => req.IsCorrect ? 10 : 2
        };

    /// <summary>Maps total XP to a level using the threshold table.</summary>
    internal static int ComputeLevel(int totalXp)
    {
        for (var i = 1; i < LevelThresholds.Length; i++)
        {
            if (totalXp < LevelThresholds[i]) return i;
        }
        return LevelThresholds.Length;
    }

    /// <summary>Returns the XP delta needed to reach the next level from the current one.</summary>
    internal static int XpToNextLevel(int currentLevel, int totalXp)
    {
        if (currentLevel >= LevelThresholds.Length) return 0;
        var nextThreshold = LevelThresholds[currentLevel];
        return Math.Max(0, nextThreshold - totalXp);
    }
}
