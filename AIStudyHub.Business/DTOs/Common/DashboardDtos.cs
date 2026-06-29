namespace AIStudyHub.Business.DTOs.Common;

/// <summary>
/// Aggregated KPIs for the dashboard top row. Plan A.1 + B.2.1:
/// cumulative study time, cards reviewed, average accuracy, efficiency score,
/// current streak, and flashcards due today.
/// </summary>
public sealed record DashboardKpiDto(
    int TotalStudyHours,
    int TotalStudyMinutes,
    int CardsReviewed,
    double AverageAccuracy,
    int EfficiencyScore,
    int CurrentStreakDays,
    int? FlashcardsDueToday);

/// <summary>
/// Single data point in the 14-day chart. <c>AccuracyPercent</c> is null when
/// the user didn't submit a quiz that day (Plan B.3.1).
/// </summary>
public sealed record DashboardChartPointDto(
    DateOnly Date,
    double? AccuracyPercent,
    int CardsCount);

/// <summary>Per-subject average accuracy for the past 14 days (Plan A.1).
/// MasteryPercent is rounded to one decimal so the chart label is stable.</summary>
public sealed record DashboardSubjectMasteryDto(
    Guid SubjectId,
    string SubjectCode,
    string SubjectName,
    double MasteryPercent);

/// <summary>
/// One AI-generated coaching tip. <c>Severity</c> drives the colour on the
/// frontend ("info" | "warning" | "danger"). Plan A.1 + B.2.1.
/// </summary>
public sealed record AiTipDto(
    string Severity,
    string Title,
    string Message);

/// <summary>
/// Top-level dashboard aggregate (Plan A.1). Returned by
/// <c>GET /api/Analytics/dashboard</c>.
/// </summary>
public sealed record DashboardDto(
    DashboardKpiDto Kpis,
    IReadOnlyList<DashboardChartPointDto> AccuracyTrend,
    IReadOnlyList<DashboardChartPointDto> CardsReviewedTrend,
    IReadOnlyList<DashboardSubjectMasteryDto> SubjectMasteries,
    IReadOnlyList<AiTipDto> AiTips);
