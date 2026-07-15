using AIStudyHub.Business.DTOs.Admin;
using AIStudyHub.Data.Enums;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IAdminService
{
    Task<AdminViolationListResultDto> GetViolationListAsync(
        AdminViolationListRequestDto filter, CancellationToken ct = default);

    Task<AdminDocumentReportsDto?> GetDocumentReportsAsync(
        Guid documentId, CancellationToken ct = default);

    Task<BanDocumentResultDto> BanDocumentAsync(
        Guid documentId, Guid adminUserId, CancellationToken ct = default);

    Task<UnbanDocumentResultDto> UnbanDocumentAsync(
        Guid documentId, Guid adminUserId, CancellationToken ct = default);

    Task DeleteDocumentAsync(
        Guid documentId, Guid deletedByUserId, CancellationToken ct = default);

    Task<AdminDashboardDto> GetDashboardAsync(CancellationToken ct = default);

    Task<RevenueResultDto> GetRevenueAsync(RevenueRequestDto request, CancellationToken ct = default);
}
