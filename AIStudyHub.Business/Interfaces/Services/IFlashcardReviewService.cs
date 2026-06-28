using AIStudyHub.Business.DTOs.Common;
using AIStudyHub.Business.DTOs.FlashcardReviews;
using AIStudyHub.Data.Enums;

namespace AIStudyHub.Business.Interfaces.Services;

/// <summary>
/// Spaced Repetition System (SM-2) operations on flashcards.
/// All writes upsert a single FlashcardReview row per (user, flashcard).
/// </summary>
public interface IFlashcardReviewService
{
    Task<ServiceResult<FlashcardReviewResponseDto>> ProcessReviewAsync(
        Guid userId,
        Guid flashcardId,
        ReviewQuality quality,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<DueFlashcardDto>>> GetDueAsync(
        Guid userId,
        int maxResults,
        CancellationToken cancellationToken = default);

    Task<int> CountDueAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<ServiceResult<FlashcardReviewStatsDto>> GetStatsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
