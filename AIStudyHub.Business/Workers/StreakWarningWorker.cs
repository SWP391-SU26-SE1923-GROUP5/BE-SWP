using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Data;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AIStudyHub.Business.Workers;

/// <summary>
/// Runs daily at 12:00 UTC (noon) and notifies users with an active streak who have not
/// studied yet today that their streak is at risk of being reset at 23:59 UTC.
/// Logic was extracted from DailyStreakResetWorker (which now only resets).
/// </summary>
public sealed class StreakWarningWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StreakWarningWorker> _logger;
    private DateTime _lastRunDate = DateTime.MinValue.Date;

    public StreakWarningWorker(IServiceScopeFactory scopeFactory, ILogger<StreakWarningWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("StreakWarningWorker started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            if (now.Date != _lastRunDate && now.Hour >= 12)
            {
                await RunOnceAsync(stoppingToken);
                _lastRunDate = now.Date;
            }
            // Wake up every hour to check if it's time to run
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notifier = scope.ServiceProvider.GetService<IRealTimeNotificationService>();
        if (notifier is null) return;

        var today = DateTime.UtcNow.Date;
        var stale = await db.UserStats
            .Where(s => s.CurrentStreak > 0
                        && (s.LastActivityDate == null || s.LastActivityDate.Value.Date < today))
            .Select(s => new { s.UserId, s.CurrentStreak })
            .ToListAsync(ct);

        foreach (var stats in stale)
        {
            try
            {
                // Also persist a Notification row for history
                db.Notifications.Add(new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = stats.UserId,
                    Title = "Streak at risk!",
                    Message = $"Your {stats.CurrentStreak}-day streak ends in 11h. Review a flashcard now.",
                    IsRead = false,
                    Type = NotificationType.System,
                    CreatedAt = DateTime.UtcNow
                });
                await notifier.NotifyStreakAtRiskAsync(stats.UserId, stats.CurrentStreak, 11, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to warn user {UserId}", stats.UserId);
            }
        }
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("StreakWarningWorker: notified {Count} users.", stale.Count);
    }
}
