using AIStudyHub.Data.Enums;

namespace AIStudyHub.Business.DTOs.Reports;

public sealed record ReportResponseDto(Guid Id, Guid UserId, Guid DocumentId, string Reason, string? Details, ReportStatus Status, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record CreateReportRequestDto(Guid UserId, Guid DocumentId, string Reason, string? Details);

public sealed record UpdateReportRequestDto(ReportStatus Status);
