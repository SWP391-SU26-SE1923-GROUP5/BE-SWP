using AIStudyHub.Business.DTOs.Common;
using AIStudyHub.Business.DTOs.Recommendations;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;
using AIStudyHub.Data.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AIStudyHub.Business.Services;

public sealed class RecommendationService : IRecommendationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RecommendationService> _logger;

    public RecommendationService(IUnitOfWork unitOfWork, ILogger<RecommendationService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ServiceResult<IReadOnlyList<SubjectMasteryDto>>> GetSubjectMasteryAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            return ServiceResult<IReadOnlyList<SubjectMasteryDto>>.Fail("User id is required.");

        var raw = await _unitOfWork.StudyLogs
            .Query()
            .Where(l => l.UserId == userId && l.SubjectCode != null)
            .GroupBy(l => l.SubjectCode!)
            .Select(g => new
            {
                SubjectCode = g.Key,
                Total = g.Count(),
                Correct = g.Count(l => l.IsCorrect)
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var subjectNames = await _unitOfWork.Subjects
            .Query()
            .AsNoTracking()
            .ToDictionaryAsync(s => s.SubjectCode, s => s.SubjectName, cancellationToken);

        var masteries = raw
            .Select(r => new SubjectMasteryDto(
                r.SubjectCode,
                subjectNames.TryGetValue(r.SubjectCode, out var name) ? name : r.SubjectCode,
                r.Total == 0 ? 0d : Math.Round((double)r.Correct / r.Total * 100, 2),
                r.Total,
                r.Correct))
            .OrderByDescending(m => m.TotalAttempts)
            .ToList();

        return ServiceResult<IReadOnlyList<SubjectMasteryDto>>.Ok(masteries);
    }

    public async Task<ServiceResult<RecommendationResultDto>> GetRecommendationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var masteryResult = await GetSubjectMasteryAsync(userId, cancellationToken);
        if (!masteryResult.Success)
            return ServiceResult<RecommendationResultDto>.Fail(masteryResult.Error ?? "Failed to compute mastery.");

        var masteries = masteryResult.Data ?? new List<SubjectMasteryDto>();

        if (masteries.Count == 0)
        {
            return ServiceResult<RecommendationResultDto>.Ok(new RecommendationResultDto(
                Array.Empty<SubjectMasteryDto>(),
                new[] { "Start by submitting a quiz or reviewing some flashcards to see personalized recommendations." },
                "Not enough activity yet to build recommendations."));
        }

        var recommendations = new List<string>();
        var weakSubjects = masteries.Where(m => m.MasteryPercentage < 60).OrderBy(m => m.MasteryPercentage).Take(3).ToList();
        var strongSubjects = masteries.Where(m => m.MasteryPercentage >= 80).OrderByDescending(m => m.MasteryPercentage).Take(3).ToList();

        foreach (var weak in weakSubjects)
        {
            recommendations.Add($"Focus on {weak.SubjectName} - your mastery is at {weak.MasteryPercentage:0.##}% over {weak.TotalAttempts} attempt(s). Try reviewing its flashcards today.");
        }

        foreach (var strong in strongSubjects)
        {
            recommendations.Add($"Keep your {strong.SubjectName} edge sharp - {strong.MasteryPercentage:0.##}% mastery. Spaced repetition will hold the line.");
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add("You're between 60% and 80% in every subject - a small daily commitment will push you into mastery territory.");
        }

        var summary = $"Tracked across {masteries.Count} subject(s). Weakest: {(weakSubjects.FirstOrDefault()?.SubjectName ?? "n/a")}. Strongest: {(strongSubjects.FirstOrDefault()?.SubjectName ?? "n/a")}.";

        return ServiceResult<RecommendationResultDto>.Ok(new RecommendationResultDto(
            masteries,
            recommendations,
            summary));
    }

    public async Task<IReadOnlyList<RecommendationResponseDto>> GetMyActiveRecommendationsAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var recs = await _unitOfWork.Recommendations
            .Query()
            .Where(r => r.UserId == userId && r.Status == "Active")
            .OrderByDescending(r => r.CreatedAt)
            .AsNoTracking()
            .ToListAsync(ct);

        return recs.Select(r => new RecommendationResponseDto(
            r.Id, r.UserId, r.Type.ToString(), r.ReferenceId,
            r.Title, r.Description, r.ActionUrl, r.Status,
            r.DismissedAt, r.CreatedAt, r.UpdatedAt)).ToList();
    }

    public async Task DismissAsync(Guid recommendationId, Guid userId, CancellationToken ct = default)
    {
        var rec = await _unitOfWork.Recommendations
            .Query()
            .FirstOrDefaultAsync(r => r.Id == recommendationId && r.UserId == userId, ct);
        if (rec is null) return;

        rec.Status = "Dismissed";
        rec.DismissedAt = DateTime.UtcNow;
        _unitOfWork.Recommendations.Update(rec);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private const int LeechLapsesThreshold = 4;

    public async Task CreateWeakSubjectRecommendationAsync(
        Guid userId,
        Guid subjectId,
        string subjectName,
        string subjectCode,
        double masteryPct,
        CancellationToken ct = default)
    {
        var exists = await _unitOfWork.Recommendations.Query()
            .AnyAsync(r => r.UserId == userId
                        && r.Type == RecommendationType.WeakSubject
                        && r.ReferenceId == subjectId
                        && r.Status == "Active", ct);
        if (exists) return;

        var rec = new Recommendation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = RecommendationType.WeakSubject,
            ReferenceId = subjectId,
            Title = $"Focus on {subjectName}",
            Description = $"Your mastery in {subjectName} ({subjectCode}) is {masteryPct:F1}%. Review flashcards to improve.",
            ActionUrl = $"/subjects/{subjectCode}",
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Recommendations.AddAsync(rec, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        _logger.LogInformation("Created WeakSubject recommendation for user {UserId}, subject {SubjectCode}", userId, subjectCode);
    }

    public async Task CreateLeechCardRecommendationAsync(
        Guid userId,
        Guid flashcardId,
        CancellationToken ct = default)
    {
        var exists = await _unitOfWork.Recommendations.Query()
            .AnyAsync(r => r.UserId == userId
                        && r.Type == RecommendationType.LeechCard
                        && r.ReferenceId == flashcardId
                        && r.Status == "Active", ct);
        if (exists) return;

        var rec = new Recommendation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = RecommendationType.LeechCard,
            ReferenceId = flashcardId,
            Title = "Difficult flashcard detected",
            Description = $"A flashcard has been answered incorrectly {LeechLapsesThreshold}+ times. Consider spending extra time on it.",
            ActionUrl = $"/flashcards/{flashcardId}",
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Recommendations.AddAsync(rec, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        _logger.LogInformation("Created LeechCard recommendation for user {UserId}, card {FlashcardId}", userId, flashcardId);
    }
}
