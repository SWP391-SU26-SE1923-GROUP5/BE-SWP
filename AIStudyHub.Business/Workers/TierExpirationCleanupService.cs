using AIStudyHub.Business.Options;
using AIStudyHub.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIStudyHub.Business.Workers;

public sealed class TierExpirationCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TierExpirationCleanupService> _logger;
    private readonly TimeSpan _checkInterval;

    public TierExpirationCleanupService(
        IServiceProvider serviceProvider,
        ILogger<TierExpirationCleanupService> logger,
        IOptions<CleanupOptions> cleanupOptions)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _checkInterval = TimeSpan.FromHours(cleanupOptions.Value.TierExpirationCheckIntervalHours);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Tier expiration cleanup service started. Check interval: {Interval} hours", _checkInterval.TotalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
                await DowngradeExpiredTiersAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during tier expiration cleanup");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    private async Task DowngradeExpiredTiersAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var freeTierId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var expiredUsers = await dbContext.Users
            .Where(u => u.TierExpireAt.HasValue
                && u.TierExpireAt < DateTime.UtcNow
                && u.TierId != freeTierId)
            .ToListAsync(cancellationToken);

        if (expiredUsers.Count == 0)
        {
            _logger.LogInformation("No expired tiers to clean up");
            return;
        }

        foreach (var user in expiredUsers)
        {
            _logger.LogInformation(
                "Downgrading user {UserId} from tier {OldTierId} to Free tier (expired at {ExpireAt})",
                user.Id, user.TierId, user.TierExpireAt);

            user.TierId = freeTierId;
            user.TierExpireAt = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Downgraded {Count} users with expired tiers to Free tier", expiredUsers.Count);
    }
}
