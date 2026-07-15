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
/// Scans users whose AI token usage is ≥ 80% of their tier quota and sends a warning notification
/// if they haven't been warned today. Runs daily at 09:00 UTC.
/// </summary>
public sealed class QuotaWarningWorker : BackgroundService
{
    private const double WarnThreshold = 0.80;
    private static readonly TimeSpan ScanInterval = TimeSpan.FromHours(24);
    private DateTime _lastRunDate = DateTime.MinValue.Date;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QuotaWarningWorker> _logger;

    public QuotaWarningWorker(IServiceScopeFactory scopeFactory, ILogger<QuotaWarningWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("QuotaWarningWorker started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                if (now.Date != _lastRunDate && now.Hour >= 9)
                {
                    await RunOnceAsync(stoppingToken);
                    _lastRunDate = now.Date;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "QuotaWarningWorker iteration failed.");
            }

            try
            {
                await Task.Delay(ScanInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notifier = scope.ServiceProvider.GetService<IRealTimeNotificationService>();

        var today = DateTime.UtcNow.Date;

        var users = await db.Users
            .Include(u => u.TierMembership)
            .Where(u => u.TierMembership != null && u.CurrentAiTokenUsage > 0)
            .ToListAsync(ct);

        int warned = 0;
        foreach (var user in users)
        {
            var maxTokens = user.TierMembership!.AiTokens;
            if (maxTokens <= 0 || (double)user.CurrentAiTokenUsage / maxTokens < WarnThreshold)
                continue;

            // Check idempotency: already warned today
            var warnedToday = await db.Notifications
                .AnyAsync(n => n.UserId == user.Id
                            && n.Title == "AI Token Quota Warning"
                            && n.CreatedAt >= today, ct);
            if (warnedToday) continue;

            var remaining = maxTokens - user.CurrentAiTokenUsage;
            var pct = Math.Round((double)user.CurrentAiTokenUsage / maxTokens * 100, 1);

            db.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Title = "AI Token Quota Warning",
                Message = $"You have used {pct}% of your AI token quota ({user.CurrentAiTokenUsage}/{maxTokens}). Only {remaining} tokens remaining.",
                IsRead = false,
                Type = NotificationType.System,
                CreatedAt = DateTime.UtcNow
            });
            warned++;
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("QuotaWarningWorker: warned {Count} users.", warned);
    }
}
