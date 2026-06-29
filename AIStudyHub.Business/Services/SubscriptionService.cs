using AIStudyHub.Business.DTOs.Common;
using AIStudyHub.Business.DTOs.Subscriptions;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Data.Enums;
using AIStudyHub.Data.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AIStudyHub.Business.Services;

/// <summary>
/// Reads User.TierId + TierExpireAt + the Payment history to produce the subscription
/// dashboard. Pure read-only.
/// </summary>
public sealed class SubscriptionService : ISubscriptionService
{
    /// <summary>Threshold for "expiring soon" — used by both this service and TierExpiryWorker.</summary>
    public const int ExpiryWarningDays = 3;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SubscriptionService> _logger;

    public SubscriptionService(IUnitOfWork unitOfWork, ILogger<SubscriptionService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ServiceResult<MySubscriptionDto>> GetMySubscriptionAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            return ServiceResult<MySubscriptionDto>.Fail("User id is required.");

        var user = await _unitOfWork.Users.Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
            return ServiceResult<MySubscriptionDto>.Fail("User not found.");

        var tier = user.TierId != Guid.Empty
            ? await _unitOfWork.TierMemberships.Query()
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == user.TierId, cancellationToken)
            : null;

        var today = DateTime.UtcNow.Date;
        DateTime? expiresAt = user.TierExpireAt;
        int? daysRemaining = expiresAt.HasValue
            ? (int)Math.Ceiling((expiresAt.Value.Date - today).TotalDays)
            : null;
        bool isExpiringSoon = daysRemaining.HasValue
                               && daysRemaining.Value >= 0
                               && daysRemaining.Value <= ExpiryWarningDays;
        bool isExpired = daysRemaining.HasValue && daysRemaining.Value < 0;

        var current = new CurrentSubscriptionDto(
            user.TierId,
            tier?.TierName ?? "Free",
            StartedAt: null,
            ExpiresAt: expiresAt,
            DaysRemaining: daysRemaining,
            IsExpiringSoon: isExpiringSoon,
            IsExpired: isExpired);

        var payments = await _unitOfWork.Payments.Query()
            .Where(p => p.UserId == userId
                        && p.TierId.HasValue
                        && p.Status == PaymentStatus.Completed)
            .OrderByDescending(p => p.PaymentDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var tierLookup = await _unitOfWork.TierMemberships.Query()
            .Where(t => payments.Select(p => p.TierId!.Value).Distinct().Contains(t.Id))
            .Select(t => new { t.Id, t.TierName })
            .ToDictionaryAsync(t => t.Id, t => t.TierName, cancellationToken);

        var history = payments.Select(p => new SubscriptionHistoryItemDto(
            p.Id,
            p.TierId!.Value,
            tierLookup.TryGetValue(p.TierId!.Value, out var name) ? name : "Unknown",
            p.Amount,
            p.PaymentDate,
            ExpiresAt: null)).ToList();

        return ServiceResult<MySubscriptionDto>.Ok(new MySubscriptionDto(current, history));
    }
}