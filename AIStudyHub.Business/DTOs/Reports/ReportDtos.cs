namespace AIStudyHub.Business.DTOs.Reports;

public sealed record ReportResponseDto(Guid Id, Guid UserId, Guid DocumentId, string? Reason, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record CreateReportRequestDto(Guid UserId, Guid DocumentId, string? Reason);

public sealed record UpdateReportRequestDto(string? Reason);
