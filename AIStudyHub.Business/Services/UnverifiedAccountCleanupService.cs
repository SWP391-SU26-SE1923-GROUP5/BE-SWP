using AIStudyHub.Business.Options;
using AIStudyHub.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIStudyHub.Business.Services;

public sealed class UnverifiedAccountCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<UnverifiedAccountCleanupService> _logger;
    private readonly int _retentionDays;

    public UnverifiedAccountCleanupService(
        IServiceProvider serviceProvider,
        ILogger<UnverifiedAccountCleanupService> logger,
        IOptions<CleanupOptions> cleanupOptions)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _retentionDays = cleanupOptions.Value.UnverifiedAccountRetentionDays;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var nextRun = now.Date.AddDays(1);
            var delay = nextRun - now;

            _logger.LogInformation("Unverified account cleanup scheduled for {NextRun} UTC", nextRun);

            try
            {
                await Task.Delay(delay, stoppingToken);
                await CleanupUnverifiedAccountsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during unverified account cleanup");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    private async Task CleanupUnverifiedAccountsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var cutoffDate = DateTime.UtcNow.AddDays(-_retentionDays);

        var unverifiedUsers = await dbContext.Users
            .Where(u => !u.EmailConfirmed && u.CreatedAt < cutoffDate)
            .ToListAsync(cancellationToken);

        if (unverifiedUsers.Count == 0)
        {
            _logger.LogInformation("No unverified accounts to clean up");
            return;
        }

        var userIds = unverifiedUsers.Select(u => u.Id).ToList();

        var otpRecords = await dbContext.OtpRecords
            .Where(o => userIds.Contains(o.UserId))
            .ToListAsync(cancellationToken);
        dbContext.OtpRecords.RemoveRange(otpRecords);

        var documents = await dbContext.Documents
            .Where(d => userIds.Contains(d.UserId))
            .ToListAsync(cancellationToken);
        var documentIds = documents.Select(d => d.Id).ToList();

        var documentChunks = await dbContext.DocumentChunks
            .Where(c => documentIds.Contains(c.DocumentId))
            .ToListAsync(cancellationToken);
        dbContext.DocumentChunks.RemoveRange(documentChunks);

        dbContext.Documents.RemoveRange(documents);

        var flashcards = await dbContext.Flashcards
            .Where(f => documentIds.Contains(f.DocumentId))
            .ToListAsync(cancellationToken);
        dbContext.Flashcards.RemoveRange(flashcards);

        var userRoles = await dbContext.UserRoles
            .Where(ur => userIds.Contains(ur.UserId))
            .ToListAsync(cancellationToken);
        dbContext.UserRoles.RemoveRange(userRoles);

        var notifications = await dbContext.Notifications
            .Where(n => userIds.Contains(n.UserId))
            .ToListAsync(cancellationToken);
        dbContext.Notifications.RemoveRange(notifications);

        dbContext.Users.RemoveRange(unverifiedUsers);

        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cleaned up {Count} unverified accounts older than {Days} days", unverifiedUsers.Count, _retentionDays);
    }
}
