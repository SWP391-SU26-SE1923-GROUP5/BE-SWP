using AIStudyHub.Business.DTOs.Common;
using AIStudyHub.Business.DTOs.Subscriptions;

namespace AIStudyHub.Business.Interfaces.Services;

/// <summary>
/// Plan C4 — exposes the current user's tier status and payment history.
/// </summary>
public interface ISubscriptionService
{
    Task<ServiceResult<MySubscriptionDto>> GetMySubscriptionAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}