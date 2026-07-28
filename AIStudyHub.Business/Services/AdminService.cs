using AIStudyHub.Business.DTOs.Admin;
using AIStudyHub.Business.DTOs.Reports;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;
using AIStudyHub.Data.Interfaces;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace AIStudyHub.Business.Services;

public sealed class AdminService : IAdminService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public AdminService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<AdminViolationListResultDto> GetViolationListAsync(
        AdminViolationListRequestDto filter, CancellationToken ct = default)
    {
        var query = _unitOfWork.Reports.Query()
            .AsNoTracking()
            .Include(r => r.Document)
            .ThenInclude(d => d!.User)
            .AsNoTracking()
            .AsQueryable();

        if (filter.UserId.HasValue)
            query = query.Where(r => r.Document!.UserId == filter.UserId.Value);

        if (filter.Status.HasValue)
            query = query.Where(r => r.Document!.Status == filter.Status.Value);

        var total = await query.Select(r => r.DocumentId).Distinct().CountAsync(ct);

        IQueryable<Document> orderedDocs;
        switch (filter.SortBy)
        {
            case ViolationSortBy.CreatedAt:
                orderedDocs = filter.SortDescending
                    ? _unitOfWork.Documents.Query().Where(d => d.LifecycleStatus == DocumentLifecycleStatus.Active)
                        .Where(d => _unitOfWork.Reports.Query().Any(r => r.DocumentId == d.Id))
                        .OrderByDescending(d => d.CreatedAt)
                    : _unitOfWork.Documents.Query().Where(d => d.LifecycleStatus == DocumentLifecycleStatus.Active)
                        .Where(d => _unitOfWork.Reports.Query().Any(r => r.DocumentId == d.Id))
                        .OrderBy(d => d.CreatedAt);
                break;

            case ViolationSortBy.LatestReportAt:
                orderedDocs = filter.SortDescending
                    ? _unitOfWork.Documents.Query().Where(d => d.LifecycleStatus == DocumentLifecycleStatus.Active)
                        .Where(d => _unitOfWork.Reports.Query().Any(r => r.DocumentId == d.Id))
                        .OrderByDescending(d => _unitOfWork.Reports.Query()
                            .Where(r => r.DocumentId == d.Id).Max(r => (DateTime?)r.CreatedAt))
                    : _unitOfWork.Documents.Query().Where(d => d.LifecycleStatus == DocumentLifecycleStatus.Active)
                        .Where(d => _unitOfWork.Reports.Query().Any(r => r.DocumentId == d.Id))
                        .OrderBy(d => _unitOfWork.Reports.Query()
                            .Where(r => r.DocumentId == d.Id).Min(r => (DateTime?)r.CreatedAt));
                break;

            case ViolationSortBy.ReportCount:
            default:
                orderedDocs = filter.SortDescending
                    ? _unitOfWork.Documents.Query().Where(d => d.LifecycleStatus == DocumentLifecycleStatus.Active)
                        .Where(d => _unitOfWork.Reports.Query().Any(r => r.DocumentId == d.Id))
                        .OrderByDescending(d => _unitOfWork.Reports.Query().Count(r => r.DocumentId == d.Id))
                    : _unitOfWork.Documents.Query().Where(d => d.LifecycleStatus == DocumentLifecycleStatus.Active)
                        .Where(d => _unitOfWork.Reports.Query().Any(r => r.DocumentId == d.Id))
                        .OrderBy(d => _unitOfWork.Reports.Query().Count(r => r.DocumentId == d.Id));
                break;
        }

        var pagedIds = orderedDocs
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(d => d.Id)
            .ToListAsync(ct);

        var violations = await query
            .Where(r => pagedIds.Result.Contains(r.DocumentId))
            .Include(r => r.Document)
            .ThenInclude(d => d!.User)
            .AsNoTracking()
            .ToListAsync(ct);

        var grouped = violations
            .Where(r => r.Document != null)
            .GroupBy(r => r.DocumentId)
            .Select(g =>
            {
                var doc = g.First().Document!;
                var dominantCategory = g
                    .GroupBy(r => r.Category)
                    .OrderByDescending(cg => cg.Count())
                    .Select(cg => (ReportCategoryDto)cg.Key)
                    .FirstOrDefault();
                return new AdminViolationDto(
                    DocumentId: doc.Id,
                    UserId: doc.UserId,
                    OwnerEmail: doc.User?.Email ?? string.Empty,
                    OwnerFullName: doc.User?.FullName ?? string.Empty,
                    Title: doc.Title ?? string.Empty,
                    Status: doc.Status ?? DocumentStatus.Draft,
                    ReportCount: g.Count(),
                    DominantCategory: dominantCategory,
                    CreatedAt: doc.CreatedAt,
                    LatestReportAt: g.Max(r => r.CreatedAt));
            })
            .ToList();

        return new AdminViolationListResultDto(
            Items: violations.GroupBy(r => r.DocumentId)
                .Select(g =>
                {
                    var doc = g.First().Document!;
                    var dominantCategory = g
                        .GroupBy(r => r.Category)
                        .OrderByDescending(cg => cg.Count())
                        .Select(cg => (ReportCategoryDto)cg.Key)
                        .FirstOrDefault();
                    return new AdminViolationDto(
                        DocumentId: doc.Id,
                        UserId: doc.UserId,
                        OwnerEmail: doc.User?.Email ?? string.Empty,
                        OwnerFullName: doc.User?.FullName ?? string.Empty,
                        Title: doc.Title ?? string.Empty,
                        Status: doc.Status ?? DocumentStatus.Draft,
                        ReportCount: g.Count(),
                        DominantCategory: dominantCategory,
                        CreatedAt: doc.CreatedAt,
                        LatestReportAt: g.Max(r => r.CreatedAt));
                })
                .ToList(),
            TotalCount: total,
            Page: filter.Page,
            PageSize: filter.PageSize);
    }

    public async Task<AdminDocumentReportsDto?> GetDocumentReportsAsync(
        Guid documentId, CancellationToken ct = default)
    {
        var document = await _unitOfWork.Documents.Query()
            .Include(d => d.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId, ct);

        if (document == null)
            return null;

        var reports = await _unitOfWork.Reports.Query()
            .Include(r => r.User)
            .Include(r => r.Document)
            .Include(r => r.ResolvedByUser)
            .AsNoTracking()
            .Where(r => r.DocumentId == documentId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return new AdminDocumentReportsDto(
            DocumentId: document.Id,
            DocumentTitle: document.Title ?? string.Empty,
            DocumentOwnerId: document.UserId,
            DocumentOwnerEmail: document.User?.Email ?? string.Empty,
            DocumentOwnerFullName: document.User?.FullName ?? string.Empty,
            DocumentStatus: document.Status ?? DocumentStatus.Draft,
            TotalReports: reports.Count,
            Reports: reports.Select(_mapper.Map<ReportResponseDto>).ToList());
    }

    public async Task<BanDocumentResultDto> BanDocumentAsync(
        Guid documentId, Guid adminUserId, CancellationToken ct = default)
    {
        var document = await _unitOfWork.Documents.Query()
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == documentId, ct);

        if (document == null)
            throw new KeyNotFoundException($"Document with ID {documentId} not found.");

        document.Status = DocumentStatus.Banned;
        document.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(ct);

        var reports = await _unitOfWork.Reports.Query()
            .Include(r => r.User)
            .Include(r => r.ResolvedByUser)
            .AsNoTracking()
            .Where(r => r.DocumentId == documentId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return new BanDocumentResultDto(
            DocumentId: document.Id,
            Title: document.Title ?? string.Empty,
            OwnerId: document.UserId,
            OwnerEmail: document.User?.Email ?? string.Empty,
            OwnerFullName: document.User?.FullName ?? string.Empty,
            Status: document.Status ?? DocumentStatus.Draft,
            TotalReports: reports.Count,
            Reports: reports.Select(r => _mapper.Map<ReportResponseDto>(r)).ToList());
    }

    public async Task<UnbanDocumentResultDto> UnbanDocumentAsync(
        Guid documentId, Guid adminUserId, CancellationToken ct = default)
    {
        var document = await _unitOfWork.Documents.Query()
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == documentId, ct);

        if (document == null)
            throw new KeyNotFoundException($"Document with ID {documentId} not found.");

        document.Status = DocumentStatus.Draft;
        document.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(ct);

        return new UnbanDocumentResultDto(
            DocumentId: document.Id,
            Title: document.Title ?? string.Empty,
            OwnerId: document.UserId,
            OwnerEmail: document.User?.Email ?? string.Empty,
            OwnerFullName: document.User?.FullName ?? string.Empty,
            Status: document.Status ?? DocumentStatus.Draft);
    }

    public async Task DeleteDocumentAsync(
        Guid documentId, Guid deletedByUserId, CancellationToken ct = default)
    {
        var document = await _unitOfWork.Documents.GetByIdAsync(documentId, ct);
        if (document == null)
            throw new KeyNotFoundException($"Document with ID {documentId} not found.");

        document.LifecycleStatus = DocumentLifecycleStatus.Trashed;
        document.TrashedAt = DateTime.UtcNow;
        document.TrashedBy = deletedByUserId;
        document.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<AdminDashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var totalUsers = await _unitOfWork.Users.Query().CountAsync(ct);
        var documentsReported = await _unitOfWork.Reports.Query()
            .Select(r => r.DocumentId)
            .Distinct()
            .CountAsync(ct);

        return new AdminDashboardDto(totalUsers, documentsReported);
    }

    public async Task<RevenueResultDto> GetRevenueAsync(RevenueRequestDto request, CancellationToken ct = default)
    {
        var completedPayments = _unitOfWork.Payments.Query()
            .AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Completed);

        var totalRevenue = await completedPayments.SumAsync(p => p.Amount, ct);
        var totalTransactions = await completedPayments.CountAsync(ct);

        var now = DateTime.UtcNow;
        IReadOnlyList<RevenueBreakdownDto> breakdown;

        switch (request.Duration)
        {
            case RevenueDuration.Day:
                var dailyGroups = completedPayments
                    .GroupBy(p => p.PaymentDate.Date)
                    .Select(g => new RevenueBreakdownDto(g.Key, g.Sum(p => p.Amount), g.Count()))
                    .OrderByDescending(b => b.Period)
                    .Take(30)
                    .ToList();
                breakdown = dailyGroups;
                break;

            case RevenueDuration.Month:
                var monthlyGroups = completedPayments
                    .GroupBy(p => new { p.PaymentDate.Year, p.PaymentDate.Month })
                    .Select(g => new RevenueBreakdownDto(
                        new DateTime(g.Key.Year, g.Key.Month, 1), g.Sum(p => p.Amount), g.Count()))
                    .OrderByDescending(b => b.Period)
                    .Take(12)
                    .ToList();
                breakdown = monthlyGroups;
                break;

            case RevenueDuration.Year:
                var yearlyGroups = completedPayments
                    .GroupBy(p => p.PaymentDate.Year)
                    .Select(g => new RevenueBreakdownDto(
                        new DateTime(g.Key, 1, 1), g.Sum(p => p.Amount), g.Count()))
                    .OrderByDescending(b => b.Period)
                    .ToList();
                breakdown = yearlyGroups;
                break;

            default:
                breakdown = [];
                break;
        }

        return new RevenueResultDto(totalRevenue, totalTransactions, request.Duration, breakdown);
    }
}
