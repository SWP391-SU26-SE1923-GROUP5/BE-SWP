using AIStudyHub.Data;
using AIStudyHub.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AIStudyHub.Business.Workers;

/// <summary>
/// Implements the daily streak reset rule from message.txt section 3.6:
///   "Hangfire chạy một Cron Job vào 23:59 mỗi ngày. Nếu LastActivityDate là hôm nay: Giữ nguyên.
///    Nếu không: Reset CurrentStreak = 0."
///
/// Uses an interval loop (default 1h) rather than a true cron expression so it stays simple and
/// crash-tolerant. The actual reset only runs once per day by comparing the last-run date.
/// </summary>
public sealed class DailyStreakResetWorker : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DailyStreakResetWorker> _logger;
    private DateTime _lastResetDate = DateTime.MinValue.Date;

    public DailyStreakResetWorker(IServiceProvider serviceProvider, ILogger<DailyStreakResetWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DailyStreakResetWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(CheckInterval, stoppingToken);

                var now = DateTime.UtcNow;
                if (now.Date == _lastResetDate)
                    continue;

                // Per message.txt: trigger at 23:59 UTC. We allow a window of the last hour of the day
                // so the job still fires even if the host restarts right at midnight.
                if (now.Hour >= 23)
                {
                    await ResetStaleStreaksAsync(stoppingToken);
                    _lastResetDate = now.Date;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DailyStreakResetWorker iteration failed");
            }
        }
    }

    private async Task ResetStaleStreaksAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var today = DateTime.UtcNow.Date;
        var stale = await db.UserStats
            .Where(s => s.CurrentStreak > 0
                        && (s.LastActivityDate == null || s.LastActivityDate.Value.Date < today))
            .ToListAsync(cancellationToken);

        if (stale.Count == 0)
        {
            _logger.LogInformation("DailyStreakResetWorker: no stale streaks found.");
            return;
        }

        foreach (var stats in stale)
        {
            stats.CurrentStreak = 0;
            stats.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("DailyStreakResetWorker: reset streak for {Count} users.", stale.Count);
    }
}
