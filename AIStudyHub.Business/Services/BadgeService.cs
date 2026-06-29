using AIStudyHub.Business.DTOs.Gamification;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Data;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;
using AIStudyHub.Data.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AIStudyHub.Business.Services;

/// <summary>
/// Default implementation of <see cref="IBadgeService"/>. All Evaluate* methods are
/// idempotent: the (UserId, BadgeId) unique index on UserBadge means re-running
/// either silently swallows the duplicate (race condition) or simply returns without
/// awarding additional XP.
/// </summary>
public sealed class BadgeService : IBadgeService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGamificationService? _gamificationService;
    private readonly IRealTimeNotificationService? _realTimeNotifier;
    private readonly ILogger<BadgeService> _logger;

    public BadgeService(
        IUnitOfWork unitOfWork,
        ILogger<BadgeService> logger,
        IGamificationService? gamificationService = null,
        IRealTimeNotificationService? realTimeNotifier = null)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _gamificationService = gamificationService;
        _realTimeNotifier = realTimeNotifier;
    }

    public async Task<IReadOnlyList<AchievementDto>> EvaluateQuizBadgeAsync(
        Guid userId,
        QuizSubmission submission,
        CancellationToken cancellationToken = default)
    {
        if (submission is null || userId == Guid.Empty) return Array.Empty<AchievementDto>();

        var unlocked = new List<AchievementDto>();

        // SHARPSHOOTER: 100% on a quiz with at least 10 questions (Plan A.7)
        if (submission.MaxScore >= 10 && submission.TotalCorrect == submission.MaxScore)
        {
            var dto = await TryUnlockAsync(userId, BadgeCodes.Sharpshooter, cancellationToken);
            if (dto is not null) unlocked.Add(dto);
        }

        // MASTERY_MATH: ≥85% in Mathematics.
        // We need the subject of the quiz — fetch the document in the same call chain.
        var document = await _unitOfWork.Documents.Query()
            .Include(d => d.Subject)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == _unitOfWork.Quizzes.Query()
                .Where(q => q.Id == submission.QuizId)
                .Select(q => q.DocumentId)
                .FirstOrDefault(), cancellationToken);

        if (document?.Subject?.SubjectCode == "MATH" && submission.MaxScore > 0)
        {
            var percent = submission.TotalCorrect * 100m / submission.MaxScore;
            if (percent >= 85m)
            {
                var dto = await TryUnlockAsync(userId, BadgeCodes.MasteryMath, cancellationToken);
                if (dto is not null) unlocked.Add(dto);
            }
        }

        return unlocked;
    }

    public async Task<IReadOnlyList<AchievementDto>> EvaluateFlashcardBadgeAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) return Array.Empty<AchievementDto>();

        // CARDS_500: distinct flashcards reviewed at least once
        var uniqueCards = await _unitOfWork.FlashcardReviews.Query()
            .Where(r => r.UserId == userId)
            .Select(r => r.FlashcardId)
            .Distinct()
            .CountAsync(cancellationToken);

        if (uniqueCards >= 500)
        {
            var dto = await TryUnlockAsync(userId, BadgeCodes.Cards500, cancellationToken);
            return dto is null ? Array.Empty<AchievementDto>() : new[] { dto };
        }
        return Array.Empty<AchievementDto>();
    }

    public async Task<IReadOnlyList<AchievementDto>> EvaluateDocumentBadgeAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) return Array.Empty<AchievementDto>();

        // BOOKWORM: 7 documents successfully processed
        var doneCount = await _unitOfWork.Documents.Query()
            .Where(d => d.UserId == userId && d.Status == DocumentStatus.Done)
            .CountAsync(cancellationToken);

        if (doneCount >= 7)
        {
            var dto = await TryUnlockAsync(userId, BadgeCodes.Bookworm, cancellationToken);
            return dto is null ? Array.Empty<AchievementDto>() : new[] { dto };
        }
        return Array.Empty<AchievementDto>();
    }

    public async Task<IReadOnlyList<AchievementDto>> EvaluateStreakBadgeAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) return Array.Empty<AchievementDto>();

        var stats = await _unitOfWork.UserStats.Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        if (stats is null || stats.CurrentStreak < 7) return Array.Empty<AchievementDto>();

        var dto = await TryUnlockAsync(userId, BadgeCodes.Streak7D, cancellationToken);
        return dto is null ? Array.Empty<AchievementDto>() : new[] { dto };
    }

    public async Task<IReadOnlyList<AchievementDto>> GetAchievementsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var badges = await _unitOfWork.Badges.Query().AsNoTracking().ToListAsync(cancellationToken);
        var userUnlocks = await _unitOfWork.UserBadges.Query()
            .Where(ub => ub.UserId == userId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var unlockedById = userUnlocks.ToDictionary(ub => ub.BadgeId);

        var list = new List<AchievementDto>(badges.Count);
        foreach (var b in badges.OrderBy(b => b.Category).ThenBy(b => b.Title))
        {
            var isUnlocked = unlockedById.TryGetValue(b.Id, out var unlock);
            list.Add(new AchievementDto(
                b.Id,
                b.Code,
                b.Title,
                b.Description,
                b.Category,
                b.TargetValue,
                b.IconUrl,
                b.XpReward,
                IsUnlocked: isUnlocked,
                EarnedDate: isUnlocked ? unlock!.EarnedDate : null,
                CurrentProgress: await ComputeCurrentProgressAsync(userId, b.Code, cancellationToken)));
        }
        return list;
    }

    /// <summary>
    /// Try to unlock one badge for the user. Returns the AchievementDto if a NEW
    /// unlock happened (signal sent, XP awarded). Returns null if already unlocked.
    /// Catches <see cref="DbUpdateException"/> for the unique-violation race.
    /// </summary>
    private async Task<AchievementDto?> TryUnlockAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken)
    {
        var badge = await _unitOfWork.Badges.Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Code == code, cancellationToken);

        if (badge is null)
        {
            _logger.LogWarning("Badge code {Code} not found in seed data", code);
            return null;
        }

        var already = await _unitOfWork.UserBadges.Query()
            .AnyAsync(ub => ub.UserId == userId && ub.BadgeId == badge.Id, cancellationToken);

        if (already) return null;

        var userBadge = new UserBadge
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BadgeId = badge.Id,
            EarnedDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.UserBadges.AddAsync(userBadge, cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Unique-violation race: another concurrent caller already inserted.
            return null;
        }

        // Award bonus XP via the existing engine.
        if (_gamificationService is not null)
        {
            try
            {
                await _gamificationService.AwardXpAsync(
                    new DTOs.Gamification.XpAwardRequest(
                        UserId: userId,
                        XpEarned: 0,
                        IsCorrect: true,
                        ActivityType: ActivityType.BadgeEarned,
                        DocumentId: null,
                        SubjectCode: null,
                        TimeSpentSeconds: null),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to award BadgeEarned XP to user {UserId} for {Code}", userId, code);
            }
        }

        var dto = new AchievementDto(
            badge.Id,
            badge.Code,
            badge.Title,
            badge.Description,
            badge.Category,
            badge.TargetValue,
            badge.IconUrl,
            badge.XpReward,
            IsUnlocked: true,
            EarnedDate: userBadge.EarnedDate,
            CurrentProgress: badge.TargetValue);

        if (_realTimeNotifier is not null)
        {
            try
            {
                await _realTimeNotifier.NotifyBadgeEarnedAsync(userId, dto, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to broadcast badge earned to user {UserId}", userId);
            }
        }

        return dto;
    }

    /// <summary>
    /// How far the user is towards this badge, used by the achievements page
    /// to render a progress bar. Read-only — never persists anything.
    /// </summary>
    private async Task<decimal> ComputeCurrentProgressAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken)
    {
        return code switch
        {
            BadgeCodes.Streak7D => await _unitOfWork.UserStats.Query()
                .Where(s => s.UserId == userId)
                .Select(s => (decimal)s.CurrentStreak)
                .FirstOrDefaultAsync(cancellationToken),

            BadgeCodes.Cards500 => await _unitOfWork.FlashcardReviews.Query()
                .Where(r => r.UserId == userId)
                .Select(r => r.FlashcardId)
                .Distinct()
                .CountAsync(cancellationToken),

            BadgeCodes.Bookworm => await _unitOfWork.Documents.Query()
                .Where(d => d.UserId == userId && d.Status == DocumentStatus.Done)
                .CountAsync(cancellationToken),

            // Accuracy / mastery badges are not cumulative — return 0 until unlocked.
            BadgeCodes.Sharpshooter or BadgeCodes.MasteryMath => 0m,

            _ => 0m
        };
    }
}
