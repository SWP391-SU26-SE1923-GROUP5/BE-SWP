using AIStudyHub.Business.DTOs.Common;
using AIStudyHub.Business.DTOs.FlashcardReviews;
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

    public FlashcardReviewService(IUnitOfWork unitOfWork, ILogger<FlashcardReviewService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ServiceResult<FlashcardReviewResponseDto>> ProcessReviewAsync(
        Guid userId,
        Guid flashcardId,
        ReviewQuality quality,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            return ServiceResult<FlashcardReviewResponseDto>.Fail("User id is required.");
        if (flashcardId == Guid.Empty)
            return ServiceResult<FlashcardReviewResponseDto>.Fail("Flashcard id is required.");

        var flashcard = await _unitOfWork.Flashcards.GetByIdAsync(flashcardId, cancellationToken);
        if (flashcard is null)
            return ServiceResult<FlashcardReviewResponseDto>.Fail("Flashcard not found.");

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
            return ServiceResult<FlashcardReviewResponseDto>.Fail("Could not save review.");
        }

        return ServiceResult<FlashcardReviewResponseDto>.Ok(new FlashcardReviewResponseDto(
            existing.Id,
            existing.FlashcardId,
            existing.NextReviewDate,
            existing.EaseFactor,
            existing.Interval,
            existing.Repetitions));
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
