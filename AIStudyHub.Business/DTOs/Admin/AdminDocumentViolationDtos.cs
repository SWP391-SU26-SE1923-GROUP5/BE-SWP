using AIStudyHub.Business.DTOs.Reports;
using AIStudyHub.Data.Enums;

namespace AIStudyHub.Business.DTOs.Admin;

public enum ViolationSortBy { ReportCount = 1, CreatedAt = 2, LatestReportAt = 3 }

public sealed record AdminViolationListRequestDto(
    DocumentStatus? Status,
    Guid? UserId,
    ViolationSortBy SortBy = ViolationSortBy.ReportCount,
    bool SortDescending = true,
    int Page = 1,
    int PageSize = 20);

public sealed record AdminViolationDto(
    Guid DocumentId,
    Guid UserId,
    string OwnerEmail,
    string OwnerFullName,
    string Title,
    DocumentStatus Status,
    int ReportCount,
    ReportCategoryDto? DominantCategory,
    DateTime CreatedAt,
    DateTime? LatestReportAt);

public sealed record AdminViolationListResultDto(
    IReadOnlyList<AdminViolationDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record AdminDocumentReportsDto(
    Guid DocumentId,
    string DocumentTitle,
    Guid DocumentOwnerId,
    string DocumentOwnerEmail,
    string DocumentOwnerFullName,
    DocumentStatus DocumentStatus,
    int TotalReports,
    IReadOnlyList<ReportResponseDto> Reports);

public sealed record BanDocumentResultDto(
    Guid DocumentId,
    string Title,
    Guid OwnerId,
    string OwnerEmail,
    string OwnerFullName,
    DocumentStatus Status,
    int TotalReports,
    IReadOnlyList<ReportResponseDto> Reports);

public sealed record UnbanDocumentResultDto(
    Guid DocumentId,
    string Title,
    Guid OwnerId,
    string OwnerEmail,
    string OwnerFullName,
    DocumentStatus Status);

public sealed record AdminDashboardDto(
    int TotalUsers,
    int DocumentsReported);

public enum RevenueDuration { Day = 1, Month = 2, Year = 3 }

public sealed record RevenueRequestDto(RevenueDuration Duration);

public sealed record RevenueBreakdownDto(DateTime Period, decimal Revenue, int TransactionCount);

public sealed record RevenueResultDto(
    decimal TotalRevenue,
    int TotalTransactions,
    RevenueDuration Duration,
    IReadOnlyList<RevenueBreakdownDto> Breakdown);
