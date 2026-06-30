namespace AIStudyHub.Business.DTOs.Reports;

public enum ReportCategoryDto { Spam = 1, CopyrightViolation = 2, IncorrectInformation = 3, InappropriateContent = 4, Other = 5 }
public enum ReportStatusDto { Pending = 1, Reviewed = 2, Resolved = 3, Rejected = 4 }

public sealed record ReportResponseDto(
    Guid Id, Guid UserId, string UserFullName,
    Guid DocumentId, string DocumentTitle,
    ReportCategoryDto Category, string? Reason,
    ReportStatusDto Status,
    Guid? ResolvedBy, string? ResolvedByFullName, DateTime? ResolvedAt,
    DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record CreateReportRequestDto(
    Guid DocumentId,
    ReportCategoryDto Category,
    string? Reason);

public sealed record UpdateReportStatusRequestDto(
    ReportStatusDto Status);

public sealed record ReportFilterDto(
    ReportStatusDto? Status,
    Guid? DocumentId,
    Guid? UserId,
    DateTime? FromDate,
    DateTime? ToDate,
    int Page = 1,
    int PageSize = 20);

public sealed record BulkReportIdsRequestDto(IReadOnlyList<Guid> ReportIds, ReportStatusDto Status);
public sealed record BulkDocumentIdsRequestDto(IReadOnlyList<Guid> DocumentIds);
public sealed record BulkFailureDto(Guid Id, string Reason);
public sealed record BulkReportStatusResultDto(int Updated, IReadOnlyList<BulkFailureDto> Failed);
public sealed record BulkMarkNonFlaggableResultDto(int Documents, int ReportsRejected);
