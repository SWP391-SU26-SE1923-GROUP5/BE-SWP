using System.Data;
using AIStudyHub.Business.DTOs.Common;
using AIStudyHub.Business.DTOs.FlashcardReviews;
using AIStudyHub.Business.DTOs.Gamification;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Data;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;
using AIStudyHub.Data.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<FlashcardReviewService> _logger;
    private readonly IGamificationService? _gamificationService;
    private readonly IBadgeService? _badgeService;
    private readonly IRecommendationService? _recommendationService;

    public FlashcardReviewService(
        IUnitOfWork unitOfWork,
        ApplicationDbContext dbContext,
        ILogger<FlashcardReviewService> logger,
        IGamificationService? gamificationService = null,
        IBadgeService? badgeService = null,
        IRecommendationService? recommendationService = null)
    {
        _unitOfWork = unitOfWork;
        _dbContext = dbContext;
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
        if (!Enum.IsDefined(quality))
            return ServiceResult<ReviewFlashcardResultDto>.Fail("Review quality is invalid.");
        if (timeSpentSeconds is < 1 or > 86_400)
            return ServiceResult<ReviewFlashcardResultDto>.Fail("Time spent must be between 1 and 86400 seconds.");

        var authorizedDocumentId = await _dbContext.Flashcards
            .AsNoTracking()
            .Include(card => card.FlashcardDeck)
            .Where(card => card.Id == flashcardId
                && (card.FlashcardDeck.Document.UserId == userId
                    || card.FlashcardDeck.Document.ShareStatus == "public"
                    || card.FlashcardDeck.Document.DocumentShares.Any(share => share.UserId == userId)))
            .Select(card => (Guid?)card.FlashcardDeck.DocumentId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!authorizedDocumentId.HasValue)
            return ServiceResult<ReviewFlashcardResultDto>.Fail("Flashcard not found.");

        Flashcard flashcard = null!;
        FlashcardReview existing = null!;
        FlashcardReviewAttempt attempt = null!;
        var shouldCreateLeechRecommendation = false;

        await using (var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken))
        {
            try
            {
                var lockedFlashcard = await _dbContext.Flashcards
                    .FromSqlInterpolated($"""
                        SELECT *
                        FROM [Flashcard] WITH (UPDLOCK, HOLDLOCK)
                        WHERE [card_id] = {flashcardId}
                    """)
                    .SingleOrDefaultAsync(cancellationToken);
                if (lockedFlashcard is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ServiceResult<ReviewFlashcardResultDto>.Fail("Flashcard not found.");
                }

                await _dbContext.Entry(lockedFlashcard)
                    .Reference(f => f.FlashcardDeck)
                    .LoadAsync(cancellationToken);

                if (lockedFlashcard.FlashcardDeck.DocumentId != authorizedDocumentId.Value)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ServiceResult<ReviewFlashcardResultDto>.Fail("Flashcard not found.");
                }

                await _dbContext.Entry(lockedFlashcard).ReloadAsync(cancellationToken);
                if (lockedFlashcard.FlashcardDeck.DocumentId != authorizedDocumentId.Value)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ServiceResult<ReviewFlashcardResultDto>.Fail("Flashcard not found.");
                }
                flashcard = lockedFlashcard;

                var lockedReview = await _dbContext.FlashcardReviews
                    .FromSqlInterpolated($"""
                        SELECT *
                        FROM [FlashcardReviews] WITH (UPDLOCK, HOLDLOCK)
                        WHERE [u_id] = {userId} AND [card_id] = {flashcardId}
                    """)
                    .SingleOrDefaultAsync(cancellationToken);
                if (lockedReview is not null)
                    await _dbContext.Entry(lockedReview).ReloadAsync(cancellationToken);

                if (lockedReview is null)
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
                else
                {
                    existing = lockedReview;
                }

                // Fix 1: reject same-card same-day resubmits.
                var todayUtc = DateTime.UtcNow.Date;
                var alreadyReviewedToday = await _unitOfWork.FlashcardReviewAttempts
                    .Query()
                    .AnyAsync(a => a.UserId == userId
                                && a.FlashcardId == flashcardId
                                && a.CreatedAt >= todayUtc
                                && a.CreatedAt <  todayUtc.AddDays(1),
                              cancellationToken);

                if (alreadyReviewedToday)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ServiceResult<ReviewFlashcardResultDto>.Fail(
                        "This card has already been reviewed today.");
                }

                var previousEaseFactor = existing.EaseFactor;
                var previousInterval = existing.Interval;
                var previousRepetitions = existing.Repetitions;
                var previousNextReviewDate = existing.NextReviewDate;

                ApplySm2(existing, quality);

                attempt = new FlashcardReviewAttempt
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    FlashcardId = flashcardId,
                    Quality = quality,
                    TimeSpentSeconds = timeSpentSeconds,
                    PreviousEaseFactor = previousEaseFactor,
                    ResultEaseFactor = existing.EaseFactor,
                    PreviousInterval = previousInterval,
                    ResultInterval = existing.Interval,
                    PreviousRepetitions = previousRepetitions,
                    ResultRepetitions = existing.Repetitions,
                    PreviousNextReviewDate = previousNextReviewDate,
                    ResultNextReviewDate = existing.NextReviewDate,
                    XpEarned = 0,
                    CreatedAt = DateTime.UtcNow
                };
                await _unitOfWork.FlashcardReviewAttempts.AddAsync(attempt, cancellationToken);

                if ((int)quality < 3)
                {
                    flashcard.Lapses += 1;
                    shouldCreateLeechRecommendation = flashcard.Lapses == 4;
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                await TryRollbackAsync(transaction);
                throw;
            }
            catch (Exception ex)
            {
                await TryRollbackAsync(transaction);
                _logger.LogError(
                    ex,
                    "Failed to persist FlashcardReview for user {UserId}, card {FlashcardId}",
                    userId,
                    flashcardId);
                return ServiceResult<ReviewFlashcardResultDto>.Fail("Could not save review.");
            }
        }

        var newlyUnlocked = new List<AchievementDto>();

        // Run only after the review state and immutable attempt have been saved;
        // this hook performs its own SaveChangesAsync on the shared unit of work.
        if (_recommendationService != null && shouldCreateLeechRecommendation)
        {
            try
            {
                await _recommendationService.CreateLeechCardRecommendationAsync(
                    userId,
                    flashcardId,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create leech recommendation for user {UserId}, card {FlashcardId}", userId, flashcardId);
            }
        }

        // Plan C2: award XP and accumulate TimeSpentSeconds. Wrapped so a failure
        // does not roll back the SM-2 schedule the user just saw.
        int xpEarned = 0;
        var xpAwardSucceeded = false;
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
                    xpAwardSucceeded = true;
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

        if (xpAwardSucceeded)
        {
            attempt.XpEarned = xpEarned;
            _unitOfWork.FlashcardReviewAttempts.Update(attempt);
            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to reconcile XP for flashcard review attempt {AttemptId}",
                    attempt.Id);
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

    private async Task TryRollbackAsync(IDbContextTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch (Exception rollbackException)
        {
            _logger.LogWarning(
                rollbackException,
                "Failed to roll back serialized flashcard review transaction");
        }
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
                r.Flashcard!.FlashcardDeck.DocumentId,
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

    public async Task<PagedResultDto<FlashcardReviewHistoryItemDto>> GetHistoryAsync(
        Guid userId,
        Guid? documentId,
        Guid? flashcardId,
        DateTime? fromDate,
        DateTime? toDate,
        PaginationParams pagination,
        CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.FlashcardReviewAttempts
            .Query()
            .AsNoTracking()
            .Include(attempt => attempt.Flashcard)
                .ThenInclude(flashcard => flashcard.FlashcardDeck)
                    .ThenInclude(deck => deck.Document)
                        .ThenInclude(doc => doc.Subject)
            .Where(attempt => attempt.UserId == userId);

        if (documentId.HasValue)
            query = query.Where(attempt => attempt.Flashcard.FlashcardDeck.DocumentId == documentId.Value);
        if (flashcardId.HasValue)
            query = query.Where(attempt => attempt.FlashcardId == flashcardId.Value);
        if (fromDate.HasValue)
            query = query.Where(attempt => attempt.CreatedAt >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(attempt => attempt.CreatedAt <= toDate.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var offset = Math.Max(0, pagination.Offset);
        var limit = Math.Clamp(pagination.Limit, 1, 100);
        var items = await query
            .OrderByDescending(attempt => attempt.CreatedAt)
            .ThenByDescending(attempt => attempt.Id)
            .Skip(offset)
            .Take(limit)
            .Select(attempt => new FlashcardReviewHistoryItemDto(
                attempt.Id,
                attempt.FlashcardId,
                attempt.Flashcard.FlashcardDeck.DocumentId,
                attempt.Flashcard.FlashcardDeck.Document.Title,
                attempt.Flashcard.Front,
                attempt.Quality,
                attempt.TimeSpentSeconds,
                attempt.XpEarned,
                attempt.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResultDto<FlashcardReviewHistoryItemDto>(
            items,
            totalCount,
            offset,
            limit);
    }

    public async Task<FlashcardReviewHistoryDetailDto?> GetHistoryDetailAsync(
        Guid userId,
        Guid attemptId,
        CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.FlashcardReviewAttempts
            .Query()
            .AsNoTracking()
            .Where(attempt => attempt.UserId == userId && attempt.Id == attemptId)
            .Include(attempt => attempt.Flashcard)
                .ThenInclude(flashcard => flashcard.FlashcardDeck)
                    .ThenInclude(deck => deck.Document)
                        .ThenInclude(doc => doc.Subject)
            .Select(attempt => new FlashcardReviewHistoryDetailDto(
                attempt.Id,
                attempt.FlashcardId,
                attempt.Flashcard.FlashcardDeck.DocumentId,
                attempt.Flashcard.FlashcardDeck.Document.Title,
                attempt.Flashcard.FlashcardDeck.Document.SubjectId,
                attempt.Flashcard.FlashcardDeck.Document.Subject.SubjectCode,
                attempt.Flashcard.FlashcardDeck.Document.Subject.SubjectName,
                attempt.Flashcard.Front,
                attempt.Flashcard.Back,
                attempt.Quality,
                attempt.TimeSpentSeconds,
                attempt.PreviousEaseFactor,
                attempt.ResultEaseFactor,
                attempt.PreviousInterval,
                attempt.ResultInterval,
                attempt.PreviousRepetitions,
                attempt.ResultRepetitions,
                attempt.PreviousNextReviewDate,
                attempt.ResultNextReviewDate,
                attempt.XpEarned,
                attempt.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);
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
    internal static void ApplySm2(FlashcardReview review, ReviewQuality quality, bool enableFuzzing = true)
    {
        if (quality < ReviewQuality.Good)
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

        var qClassic = quality switch
        {
            ReviewQuality.Easy => 5,
            ReviewQuality.Good => 4,
            ReviewQuality.Hard => 2,
            _ => 1
        };

        // Classic SM-2 ease factor update (using Master Spec formula):
        // EF = Max(1.3, EF + (0.1 - (5-q)*(0.08 + (5-q)*0.02)))
        var efDelta = 0.1f - (5 - qClassic) * (0.08f + (5 - qClassic) * 0.02f);
        review.EaseFactor = Math.Max(1.3f, review.EaseFactor + efDelta);

        // Fuzzing ±5%: apply to intervals >= 10 days
        if (enableFuzzing && review.Interval >= 10)
        {
            var rng = Random.Shared;
            var factor = (float)(0.95 + rng.NextDouble() * 0.10); // [0.95, 1.05)
            review.Interval = Math.Max(review.Interval, (int)Math.Round(review.Interval * factor));
        }

        // Hard cap: keep intervals from compounding beyond SQL Server's datetime range
        // and from blowing past practical review horizons.
        review.Interval = Math.Min(review.Interval, 15);

        review.NextReviewDate = DateTime.UtcNow.AddDays(review.Interval);
    }
}
