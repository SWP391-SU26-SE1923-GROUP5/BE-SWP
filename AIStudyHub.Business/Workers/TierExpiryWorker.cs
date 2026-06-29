using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;
using AIStudyHub.Data.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AIStudyHub.Business.Workers;

/// <summary>
/// Plan C4 / B.3.2 — scans the user base once per day and notifies anyone whose
/// paid tier is about to expire (≤3 days remaining). Idempotent within the same
/// day: a "tier expiring soon" notification already created today suppresses a
/// second broadcast.
/// </summary>
public sealed class TierExpiryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TierExpiryWorker> _logger;

    /// <summary>How often the worker scans. Default = 6 hours.</summary>
    public static readonly TimeSpan ScanInterval = TimeSpan.FromHours(6);

    /// <summary>Configurable via Plan C4: warn at day-3 and day-1.</summary>
    public static readonly int[] WarningDays = { 3, 1 };

    public TierExpiryWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<TierExpiryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TierExpiryWorker started (interval {Interval}, warning days {Days})",
            ScanInterval, string.Join(",", WarningDays));

        // Run once on startup, then every ScanInterval
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TierExpiryWorker tick failed; will retry next interval");
            }

            try
            {
                await Task.Delay(ScanInterval, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>
    /// Scan + notify. Public so tests can drive a single tick without waiting for the timer.
    /// </summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var unitOfWork = sp.GetRequiredService<IUnitOfWork>();
        var realTime = sp.GetRequiredService<IRealTimeNotificationService>();

        var today = DateTime.UtcNow.Date;
        var maxDate = today.AddDays(WarningDays.Max());

        // Pull candidates: anyone whose tier expires today or in the next 3 days.
        var users = await unitOfWork.Users.Query()
            .Where(u => u.TierExpireAt != null
                        && u.TierExpireAt.Value.Date >= today
                        && u.TierExpireAt.Value.Date <= maxDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (users.Count == 0) return 0;

        // Hydrate tiers with one batched query.
        var tierIds = users.Select(u => u.TierId).Where(id => id != Guid.Empty).Distinct().ToList();
        var tierLookup = tierIds.Count > 0
            ? await unitOfWork.TierMemberships.Query()
                .Where(t => tierIds.Contains(t.Id))
                .Select(t => new { t.Id, t.TierName })
                .ToDictionaryAsync(t => t.Id, t => t.TierName, cancellationToken)
            : new Dictionary<Guid, string>();

        // For idempotency, check which of these users already received a TierExpired notification today.
        var userIds = users.Select(u => u.Id).ToList();
        var alreadyNotifiedToday = await unitOfWork.Notifications.Query()
            .Where(n => userIds.Contains(n.UserId)
                        && n.Type == NotificationType.TierExpired
                        && n.CreatedAt >= today)
            .Select(n => n.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var notifiedSet = new HashSet<Guid>(alreadyNotifiedToday);

        int notificationsSent = 0;

        foreach (var user in users)
        {
            if (notifiedSet.Contains(user.Id)) continue;

            var expiresAt = user.TierExpireAt!.Value.Date;
            var daysRemaining = (int)Math.Ceiling((expiresAt - today).TotalDays);

            if (!WarningDays.Contains(daysRemaining)) continue;

            var tierName = tierLookup.TryGetValue(user.TierId, out var name) ? name : "premium";

            // 1) Persist a Notification row so it shows up in /api/Notification/my
            var note = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Type = NotificationType.TierExpired,
                Message = $"Your {tierName} plan expires in {daysRemaining} day(s). Renew now to keep premium features.",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };
            await unitOfWork.Notifications.AddAsync(note, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            // 2) Push real-time signal so a connected user gets the banner instantly
            try
            {
                await realTime.NotifyTierExpiringSoonAsync(
                    user.Id, tierName, user.TierExpireAt!.Value, daysRemaining, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "NotifyTierExpiringSoonAsync failed for {UserId}", user.Id);
            }

            notificationsSent++;
        }

        if (notificationsSent > 0)
        {
            _logger.LogInformation("TierExpiryWorker sent {Count} expiry notification(s)", notificationsSent);
        }
        return notificationsSent;
    }
}