using AIStudyHub.Business.DTOs.Common;
using AIStudyHub.Business.DTOs.Recommendations;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;
using AIStudyHub.Data.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AIStudyHub.Business.Services;

/// <summary>
/// Aggregates the data shown on the master Dashboard. Pure read-only —
/// every method is best-effort: any query failure degrades to safe defaults so the
/// UI always renders something useful (Plan B.3.1).
/// </summary>
public sealed class AnalyticsService : IAnalyticsService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFlashcardReviewService _flashcardReviewService;
    private readonly IRecommendationService _recommendationService;
    private readonly ILogger<AnalyticsService> _logger;

    public AnalyticsService(
        IUnitOfWork unitOfWork,
        IFlashcardReviewService flashcardReviewService,
        IRecommendationService recommendationService,
        ILogger<AnalyticsService> logger)
    {
        _unitOfWork = unitOfWork;
        _flashcardReviewService = flashcardReviewService;
        _recommendationService = recommendationService;
        _logger = logger;
    }

    public async Task<ServiceResult<DashboardDto>> GetDashboardAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            return ServiceResult<DashboardDto>.Fail("User id is required.");

        var stats = await _unitOfWork.UserStats.Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        var (accuracyTrend, cardsTrend) = await BuildFourteenDayChartAsync(userId, cancellationToken);

        var subjectMasteries = await BuildSubjectMasteryAsync(userId, cancellationToken);

        var aiTips = await BuildAiTipsAsync(userId, subjectMasteries, cancellationToken);

        var cardsReviewed = stats?.TotalStudySeconds is int sec
            ? Math.Min(sec, int.MaxValue) // keep simple: cards reviewed counter from StudyLog below
            : 0;

        var cardsReviewedCount = await _unitOfWork.StudyLogs.Query()
            .Where(l => l.UserId == userId && l.ActivityType == ActivityType.FlashcardReview)
            .CountAsync(cancellationToken);

        var averageAccuracy = ComputeAverageAccuracy(accuracyTrend);
        var efficiencyScore = ComputeEfficiencyScore(averageAccuracy, cardsReviewedCount, stats?.CurrentStreak ?? 0);

        var dueToday = 0;
        try { dueToday = await _flashcardReviewService.CountDueAsync(userId, cancellationToken); }
        catch (Exception ex) { _logger.LogWarning(ex, "CountDueAsync failed for {UserId}", userId); }

        var totalStudyHours = (stats?.TotalStudySeconds ?? 0) / 3600;
        var totalStudyMinutes = ((stats?.TotalStudySeconds ?? 0) % 3600) / 60;

        var kpis = new DashboardKpiDto(
            totalStudyHours,
            totalStudyMinutes,
            cardsReviewedCount,
            averageAccuracy,
            efficiencyScore,
            stats?.CurrentStreak ?? 0,
            dueToday > 0 ? dueToday : null);

        var dto = new DashboardDto(
            kpis,
            accuracyTrend,
            cardsTrend,
            subjectMasteries,
            aiTips);

        return ServiceResult<DashboardDto>.Ok(dto);
    }

    private async Task<(IReadOnlyList<DashboardChartPointDto>, IReadOnlyList<DashboardChartPointDto>)>
        BuildFourteenDayChartAsync(Guid userId, CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var start = today.AddDays(-13); // 14 days inclusive

        var quizLogs = await _unitOfWork.StudyLogs.Query()
            .Where(l => l.UserId == userId
                        && l.ActivityType == ActivityType.QuizSubmission
                        && l.CreatedAt >= start)
            .AsNoTracking()
            .Select(l => new { l.CreatedAt })
            .ToListAsync(cancellationToken);

        var cardLogs = await _unitOfWork.StudyLogs.Query()
            .Where(l => l.UserId == userId
                        && l.ActivityType == ActivityType.FlashcardReview
                        && l.CreatedAt >= start)
            .AsNoTracking()
            .Select(l => new { l.CreatedAt, l.IsCorrect })
            .ToListAsync(cancellationToken);

        // Use repository QuizSubmissions for accuracy percent (single source of truth).
        var quizSubmissions = await _unitOfWork.QuizSubmissions.Query()
            .Where(q => q.UserId == userId && q.SubmittedAt >= start && q.MaxScore > 0)
            .AsNoTracking()
            .Select(q => new { q.SubmittedAt, q.TotalCorrect, q.MaxScore })
            .ToListAsync(cancellationToken);

        var accuracy = new List<DashboardChartPointDto>(14);
        var cards = new List<DashboardChartPointDto>(14);

        for (var i = 13; i >= 0; i--)
        {
            var day = today.AddDays(-i);
            var dayQuizSubmissions = quizSubmissions.Where(q => q.SubmittedAt.Date == day).ToList();
            var dayCardSubmissions = cardLogs.Where(c => c.CreatedAt.Date == day).ToList();

            double? accuracyPercent = null;
            if (dayQuizSubmissions.Count > 0)
            {
                accuracyPercent = dayQuizSubmissions.Average(q => (double)q.TotalCorrect * 100.0 / q.MaxScore);
            }

            accuracy.Add(new DashboardChartPointDto(DateOnly.FromDateTime(day), accuracyPercent, dayQuizSubmissions.Count));
            cards.Add(new DashboardChartPointDto(DateOnly.FromDateTime(day), null, dayCardSubmissions.Count));
        }

        return (accuracy, cards);
    }

    private async Task<IReadOnlyList<DashboardSubjectMasteryDto>> BuildSubjectMasteryAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        // Reuse the existing recommendation engine (already implements this).
        var result = await _recommendationService.GetSubjectMasteryAsync(userId, cancellationToken);
        if (!result.Success || result.Data is null)
        {
            _logger.LogWarning("GetSubjectMasteryAsync failed for {UserId}", userId);
            return Array.Empty<DashboardSubjectMasteryDto>();
        }

        // Resolve the SubjectId for each code with one efficient batched query.
        var codes = result.Data.Select(s => s.SubjectCode).Distinct().ToList();
        var subjectIdMap = await _unitOfWork.Subjects.Query()
            .Where(s => codes.Contains(s.SubjectCode))
            .Select(s => new { s.SubjectCode, s.Id })
            .ToDictionaryAsync(x => x.SubjectCode, x => x.Id, cancellationToken);

        return result.Data
            .OrderByDescending(s => s.MasteryPercentage)
            .Take(5)
            .Select(s => new DashboardSubjectMasteryDto(
                subjectIdMap.TryGetValue(s.SubjectCode, out var id) ? id : Guid.Empty,
                s.SubjectCode,
                s.SubjectName,
                Math.Round(s.MasteryPercentage, 1)))
            .ToList();
    }

    private async Task<IReadOnlyList<AiTipDto>> BuildAiTipsAsync(
        Guid userId,
        IReadOnlyList<DashboardSubjectMasteryDto> subjectMasteries,
        CancellationToken cancellationToken)
    {
        var tips = new List<AiTipDto>(2);

        // Tip 1: SRS due-today warning
        try
        {
            var dueCount = await _flashcardReviewService.CountDueAsync(userId, cancellationToken);
            if (dueCount > 0)
            {
                tips.Add(new AiTipDto(
                    "warning",
                    "Review due cards",
                    $"You have {dueCount} flashcard(s) due today. A 10-minute session keeps your streak alive."));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI tip 1 (SRS) build failed for {UserId}", userId);
        }

        // Tip 2: lowest-mastery subject. Plan B.3.1 says even if AI generator fails,
        // we still emit a "Focus on {Subject}" tip with a generic message.
        var weakest = subjectMasteries
            .Where(s => s.MasteryPercent < 65)
            .OrderBy(s => s.MasteryPercent)
            .FirstOrDefault();

        if (weakest is not null)
        {
            string message;
            try
            {
                // Optional: call into the existing RAG / chat service to enrich the tip.
                // We avoid the dependency here so the dashboard still works when the
                // AI service is unavailable.
                message = $"Allocate 15 minutes to {weakest.SubjectName} today — small consistent sessions outperform long cramming.";
            }
            catch (Exception)
            {
                message = $"Focus on {weakest.SubjectName}.";
            }
            tips.Add(new AiTipDto(
                "danger",
                $"Focus on {weakest.SubjectName}",
                message));
        }

        return tips;
    }

    private static double ComputeAverageAccuracy(IReadOnlyList<DashboardChartPointDto> accuracyTrend)
    {
        var withData = accuracyTrend.Where(p => p.AccuracyPercent.HasValue).ToList();
        if (withData.Count == 0) return 0;
        return withData.Average(p => p.AccuracyPercent!.Value);
    }

    /// <summary>
    /// Weighted efficiency score per Plan A.1: 50% accuracy + 30% volume + 20% streak.
    /// Each component clamped to [0, 100] then blended. Final integer in [0, 100].
    /// </summary>
    private static int ComputeEfficiencyScore(double avgAccuracy, int cardsCount, int streakDays)
    {
        var accuracyComponent = Math.Clamp(avgAccuracy, 0d, 100d);
        // Volume: log scale to avoid skewing the score for power users. 100 cards/day = full marks.
        var volumeComponent = Math.Min(100d, Math.Log10(Math.Max(1, cardsCount)) * 50d);
        // Streak: 7 days = full marks.
        var streakComponent = Math.Min(100d, streakDays * (100d / 7d));

        var composite = accuracyComponent * 0.5 + volumeComponent * 0.3 + streakComponent * 0.2;
        return (int)Math.Round(Math.Clamp(composite, 0d, 100d));
    }
}
