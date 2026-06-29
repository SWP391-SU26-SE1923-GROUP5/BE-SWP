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

    public FlashcardReviewService(
        IUnitOfWork unitOfWork,
        ILogger<FlashcardReviewService> logger,
        IGamificationService? gamificationService = null,
        IBadgeService? badgeService = null)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _gamificationService = gamificationService;
        _badgeService = badgeService;
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
                await _gamificationService.AwardXpAsync(
                    new XpAwardRequest(
                        UserId: userId,
                        XpEarned: 0,
                        IsCorrect: isCorrect,
                        ActivityType: ActivityType.FlashcardReview,
                        DocumentId: documentId,
                        SubjectCode: subjectCode,
                        TimeSpentSeconds: timeSpentSeconds),
                    cancellationToken);
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
    /// Pure SM-2 update. Correct (Easy) increments repetitions and scales interval by ease factor;
    /// incorrect (Again/Hard) resets repetitions to 0 and pins interval to 1 day.
    /// Ease factor is always recalculated from quality, with a 1.3 floor.
    /// </summary>
    internal static void ApplySm2(FlashcardReview review, ReviewQuality quality)
    {
        var isCorrect = quality == ReviewQuality.Easy;
        var q = (int)quality; // 0..3, higher is better

        if (isCorrect)
        {
            review.Repetitions += 1;
            review.Interval = review.Repetitions switch
            {
                1 => 1,
                2 => 6,
                _ => (int)Math.Round(review.Interval * review.EaseFactor)
            };
        }
        else
        {
            review.Repetitions = 0;
            review.Interval = 1;
        }

        // Classic SM-2 ease update, anchored to q in [0..3] (we use q directly; for q == 3 the delta is +0.10).
        var delta = 0.1f - (3 - q) * (0.08f + (3 - q) * 0.02f);
        review.EaseFactor = Math.Max(1.3f, review.EaseFactor + delta);

        review.NextReviewDate = DateTime.UtcNow.AddDays(review.Interval);
    }
}
