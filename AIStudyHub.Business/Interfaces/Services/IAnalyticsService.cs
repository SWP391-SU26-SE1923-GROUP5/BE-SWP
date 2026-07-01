using AIStudyHub.Business.DTOs.Common;

namespace AIStudyHub.Business.Interfaces.Services;

/// <summary>
/// Aggregated learning analytics endpoint (Plan A.1, B.3.1).
/// Used by <c>GET /api/Analytics/dashboard</c>.
/// </summary>
public interface IAnalyticsService
{
    Task<ServiceResult<DashboardDto>> GetDashboardAsync(Guid userId, CancellationToken cancellationToken = default);
}
