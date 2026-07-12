using AIStudyHub.Business.DTOs.Common;
using AIStudyHub.Business.DTOs.FlashcardReviews;
using AIStudyHub.Business.DTOs.Gamification;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;
using AIStudyHub.Data.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AIStudyHub.Business.Services;

/// <summary>
/// Implements the SuperMemo SM-2 spaced repetition algorithm.
/// See: https://super-memory.com/english/ol/sm2.htm
///
/// Quality mapping (per message.txt section 3.5):
///   Easy = correct without hesitation, Good = correct with effort, Hard = wrong but remembered, Again = wrong
///   Only Easy/Good (>= 3 in classic SM-2) increment the streak; Hard/Again reset repetitions.
/// </summary>
public sealed class FlashcardReviewService : IFlashcardReviewService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FlashcardReviewService> _logger;
    private readonly IGamificationService? _gamificationService;
    private readonly IBadgeService? _badgeService;
    private readonly IRecommendationService? _recommendationService;

    public FlashcardReviewService(
        IUnitOfWork unitOfWork,
        ILogger<FlashcardReviewService> logger,
        IGamificationService? gamificationService = null,
        IBadgeService? badgeService = null,
        IRecommendationService? recommendationService = null)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _gamificationService = gamificationService;
        _badgeService = badgeService;
        _recommendationService = recommendationService;
    }

    public async Task<ServiceResult<ReviewFlashcardResultDto>> ProcessReviewAsync(
        Guid userId,
        Guid flashcardId,
        ReviewQuality quality,
        int? timeSpentSeconds = null,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            return ServiceResult<ReviewFlashcardResultDto>.Fail("User id is required.");
        if (flashcardId == Guid.Empty)
            return ServiceResult<ReviewFlashcardResultDto>.Fail("Flashcard id is required.");

        var flashcard = await _unitOfWork.Flashcards.GetByIdAsync(flashcardId, cancellationToken);
        if (flashcard is null)
            return ServiceResult<ReviewFlashcardResultDto>.Fail("Flashcard not found.");

        var existing = await _unitOfWork.FlashcardReviews
            .Query()
            .FirstOrDefaultAsync(r => r.UserId == userId && r.FlashcardId == flashcardId, cancellationToken);

        if (existing is null)
        {
            existing = new FlashcardReview
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                FlashcardId = flashcardId,
                EaseFactor = 2.5f,
                Interval = 1,
                Repetitions = 0,
                NextReviewDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.FlashcardReviews.AddAsync(existing, cancellationToken);
        }

        ApplySm2(existing, quality);

        // Track lapses on the flashcard (for leech detection)
        if ((int)quality < 3)
        {
            flashcard.Lapses += 1;
            _unitOfWork.Flashcards.Update(flashcard);

            // Phase 4b: auto-create LeechCard recommendation when lapses threshold (4) is first crossed
            if (_recommendationService != null && flashcard.Lapses == 4)
            {
                try
                {
                    await _recommendationService.CreateLeechCardRecommendationAsync(userId, flashcardId, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create leech recommendation for user {UserId}, card {FlashcardId}", userId, flashcardId);
                }
            }
        }

        if (existing.Id != Guid.Empty && _unitOfWork.FlashcardReviews.Query().Any(r => r.Id == existing.Id))
        {
            _unitOfWork.FlashcardReviews.Update(existing);
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Failed to persist FlashcardReview for user {UserId}, card {FlashcardId}", userId, flashcardId);
            return ServiceResult<ReviewFlashcardResultDto>.Fail("Could not save review.");
        }

        var newlyUnlocked = new List<AchievementDto>();

        // Plan C2: award XP and accumulate TimeSpentSeconds. Wrapped so a failure
        // does not roll back the SM-2 schedule the user just saw.
        int xpEarned = 0;
        if (_gamificationService is not null)
        {
            try
            {
                var documentId = flashcard.DocumentId;
                var subjectCode = await _unitOfWork.Documents.Query()
                    .Where(d => d.Id == documentId)
                    .Select(d => d.Subject.SubjectCode)
                    .FirstOrDefaultAsync(cancellationToken);

                var isCorrect = quality == ReviewQuality.Easy;
                var xpResult = await _gamificationService.AwardXpAsync(
                    new XpAwardRequest(
                        UserId: userId,
                        XpEarned: 0,
                        IsCorrect: isCorrect,
                        ActivityType: ActivityType.FlashcardReview,
                        DocumentId: documentId,
                        SubjectCode: subjectCode,
                        TimeSpentSeconds: timeSpentSeconds),
                    cancellationToken);

                if (xpResult is { Success: true, Data: not null })
                {
                    xpEarned = xpResult.Data.XpEarned;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gamification XP award failed for user {UserId}, card {FlashcardId}", userId, flashcardId);
            }
        }

        // Plan C3: badge unlock hook (Memory Master after 500 distinct cards).
        if (_badgeService is not null)
        {
            try
            {
                var unlocked = await _badgeService.EvaluateFlashcardBadgeAsync(userId, cancellationToken);
                if (unlocked.Count > 0)
                {
                    newlyUnlocked.AddRange(unlocked);
                    _logger.LogInformation("Unlocked {Count} flashcard badge(s) for user {UserId}", unlocked.Count, userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Badge evaluation failed for user {UserId}, card {FlashcardId}", userId, flashcardId);
            }
        }

        var response = new ReviewFlashcardResultDto(
            new FlashcardReviewResponseDto(
                existing.Id,
                existing.FlashcardId,
                existing.NextReviewDate,
                existing.EaseFactor,
                existing.Interval,
                existing.Repetitions),
            xpEarned,
            newlyUnlocked);

        return ServiceResult<ReviewFlashcardResultDto>.Ok(response);
    }

    public async Task<ServiceResult<IReadOnlyList<DueFlashcardDto>>> GetDueAsync(
        Guid userId,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            return ServiceResult<IReadOnlyList<DueFlashcardDto>>.Fail("User id is required.");

        var limit = maxResults <= 0 ? 50 : Math.Min(maxResults, 200);

        var due = await _unitOfWork.FlashcardReviews
            .Query()
            .Include(r => r.Flashcard)
            .Where(r => r.UserId == userId && r.NextReviewDate <= DateTime.UtcNow)
            .OrderBy(r => r.NextReviewDate)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var dtos = due
            .Where(r => r.Flashcard is not null)
            .Select(r => new DueFlashcardDto(
                r.Id,
                r.FlashcardId,
                r.Flashcard!.DocumentId,
                r.Flashcard.Front,
                r.Flashcard.Back,
                r.NextReviewDate))
            .ToList();

        return ServiceResult<IReadOnlyList<DueFlashcardDto>>.Ok(dtos);
    }

    public async Task<int> CountDueAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) return 0;
        return await _unitOfWork.FlashcardReviews
            .Query()
            .CountAsync(r => r.UserId == userId && r.NextReviewDate <= DateTime.UtcNow, cancellationToken);
    }

    public async Task<ServiceResult<FlashcardReviewStatsDto>> GetStatsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            return ServiceResult<FlashcardReviewStatsDto>.Fail("User id is required.");

        var reviews = await _unitOfWork.FlashcardReviews
            .Query()
            .Where(r => r.UserId == userId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var total = reviews.Count;
        var dueNow = reviews.Count(r => r.NextReviewDate <= DateTime.UtcNow);
        var mastered = reviews.Count(r => r.Interval >= 21);
        var avgEase = total == 0 ? 0f : reviews.Average(r => r.EaseFactor);

        return ServiceResult<FlashcardReviewStatsDto>.Ok(
            new FlashcardReviewStatsDto(total, dueNow, mastered, avgEase));
    }

    /// <summary>
    /// Pure SM-2 update with Fuzzing ±5% (Plan C3 / B.2.3).
    ///
    /// Quality mapping (per message.txt section 3.5):
    ///   Easy = correct without hesitation, Good = correct with effort, Hard = wrong but remembered, Again = wrong
    ///   Only Easy/Good (>= 3 in classic SM-2) increment the streak; Hard/Again reset repetitions.
    ///
    /// SM-2 formula per Master Spec:
    ///   q &lt; 3 → reset Repetitions=0, Interval=1
    ///   q &gt;= 3 → Repetitions++, interval 1/6/Ceil(prev*EF)
    ///   EF = Max(1.3, EF + (0.1 - (5-q)*(0.08 + (5-q)*0.02)))
    ///
    /// Fuzzing: for intervals >= 10 days, apply a ±5% random multiplier.
    /// </summary>
    internal static void ApplySm2(FlashcardReview review, ReviewQuality quality)
    {
        var q = (int)quality; // 0=Again, 1=Hard, 2=Good, 3=Easy

        if (q < 3)
        {
            // Incorrect — reset, increment lapses
            review.Repetitions = 0;
            review.Interval = 1;
            // Lapses is incremented by the caller (ProcessReviewAsync) via the flashcard entity.
        }
        else
        {
            // Correct
            review.Repetitions += 1;
            review.Interval = review.Repetitions switch
            {
                1 => 1,
                2 => 6,
                _ => (int)Math.Ceiling(review.Interval * review.EaseFactor)
            };
        }

        // Classic SM-2 ease factor update (using Master Spec formula):
        // EF = Max(1.3, EF + (0.1 - (5-q)*(0.08 + (5-q)*0.02)))
        var efDelta = 0.1f - (5 - q) * (0.08f + (5 - q) * 0.02f);
        review.EaseFactor = Math.Max(1.3f, review.EaseFactor + efDelta);

        // Fuzzing ±5%: apply to intervals >= 10 days
        if (review.Interval >= 10)
        {
            var rng = Random.Shared;
            var factor = (float)(0.95 + rng.NextDouble() * 0.10); // [0.95, 1.05)
            review.Interval = Math.Max(review.Interval, (int)Math.Round(review.Interval * factor));
        }

        review.NextReviewDate = DateTime.UtcNow.AddDays(review.Interval);
    }
}
