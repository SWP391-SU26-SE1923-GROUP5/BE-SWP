using AIStudyHub.Business.DTOs.Reports;
using AIStudyHub.Business.DTOs.Common;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IReportService
{
    Task<IReadOnlyList<ReportResponseDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ReportResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ReportResponseDto> CreateWithUserIdAsync(CreateReportRequestDto request, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<ReportResponseDto>> GetMyReportsAsync(Guid userId, CancellationToken ct = default);
    Task<PagedResultDto<ReportResponseDto>> SearchAsync(ReportFilterDto filter, CancellationToken ct = default);
    Task<ReportResponseDto> UpdateStatusAsync(Guid id, ReportStatusDto status, Guid adminUserId, CancellationToken ct = default);
    Task<int> MarkDocumentNonFlaggableAsync(Guid documentId, Guid adminUserId, CancellationToken ct = default);
    Task<BulkReportStatusResultDto> BulkUpdateStatusAsync(IReadOnlyList<Guid> ids, ReportStatusDto status, Guid adminUserId, CancellationToken ct = default);
    Task<BulkMarkNonFlaggableResultDto> BulkMarkNonFlaggableAsync(IReadOnlyList<Guid> documentIds, Guid adminUserId, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
