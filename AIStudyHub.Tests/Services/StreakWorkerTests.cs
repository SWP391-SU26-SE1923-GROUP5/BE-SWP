using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Workers;
using AIStudyHub.Data;
using AIStudyHub.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using System.Reflection;
using Xunit;

namespace AIStudyHub.Tests.Services;

public sealed class StreakWorkerTests : IDisposable
{
    private static readonly Guid FreeTierId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _dbContext;
    private readonly ServiceProvider _provider;
    private readonly Mock<IRealTimeNotificationService> _notifierMock;

    public StreakWorkerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        _dbContext = new ApplicationDbContext(options);
        _dbContext.Database.EnsureCreated();

        _notifierMock = new Mock<IRealTimeNotificationService>();
        var services = new ServiceCollection();
        services.AddSingleton(_dbContext);
        services.AddSingleton(_notifierMock.Object);
        _provider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task DailyReset_RunOnceAsync_ResetsStaleStreakWithoutWarning()
    {
        var userId = Guid.NewGuid();
        _dbContext.Users.Add(new User
        {
            Id = userId,
            FullName = "Stale streak user",
            Email = "stale@test.com",
            UserName = "stale@test.com",
            PasswordHash = "hash",
            TierId = FreeTierId,
            CreatedAt = DateTime.UtcNow
        });
        _dbContext.UserStats.Add(new UserStats
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CurrentStreak = 5,
            BestStreak = 5,
            LastActivityDate = DateTime.UtcNow.Date.AddDays(-1),
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var worker = new DailyStreakResetWorker(
            _provider,
            Mock.Of<ILogger<DailyStreakResetWorker>>());

        await worker.RunOnceAsync(CancellationToken.None);

        _dbContext.ChangeTracker.Clear();
        var persistedStats = await _dbContext.UserStats.SingleAsync(s => s.UserId == userId);
        Assert.Equal(0, persistedStats.CurrentStreak);
        _notifierMock.Verify(n => n.NotifyStreakAtRiskAsync(
            It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task StreakWarning_RunOnceAsync_PersistsAndBroadcastsOnce()
    {
        var userId = Guid.NewGuid();
        _dbContext.Users.Add(new User
        {
            Id = userId,
            FullName = "Stale streak user",
            Email = "warning@test.com",
            UserName = "warning@test.com",
            PasswordHash = "hash",
            TierId = FreeTierId,
            CreatedAt = DateTime.UtcNow
        });
        _dbContext.UserStats.Add(new UserStats
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CurrentStreak = 5,
            BestStreak = 5,
            LastActivityDate = DateTime.UtcNow.Date.AddDays(-1),
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var worker = new StreakWarningWorker(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            Mock.Of<ILogger<StreakWarningWorker>>());

        await worker.RunOnceAsync(CancellationToken.None);

        _dbContext.ChangeTracker.Clear();
        var notification = await _dbContext.Notifications.SingleAsync(n => n.UserId == userId);
        Assert.Equal("Streak at risk!", notification.Title);
        Assert.Equal("Your 5-day streak ends in 11h. Review a flashcard now.", notification.Message);
        _notifierMock.Verify(n => n.NotifyStreakAtRiskAsync(
            userId, 5, 11, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StreakWarning_ExecuteAsync_CancellationCompletesNormally()
    {
        var worker = new StreakWarningWorker(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            Mock.Of<ILogger<StreakWarningWorker>>());
        MarkAsRunToday(worker);
        using var cancellation = new CancellationTokenSource();

        var execution = InvokeExecuteAsync(worker, cancellation.Token);
        await Task.Delay(50);
        cancellation.Cancel();

        await execution.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task QuotaWarning_ExecuteAsync_CancellationCompletesNormally()
    {
        var worker = new QuotaWarningWorker(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            Mock.Of<ILogger<QuotaWarningWorker>>());
        MarkAsRunToday(worker);
        using var cancellation = new CancellationTokenSource();

        var execution = InvokeExecuteAsync(worker, cancellation.Token);
        await Task.Delay(50);
        cancellation.Cancel();

        await execution.WaitAsync(TimeSpan.FromSeconds(2));
    }

    private static Task InvokeExecuteAsync(BackgroundService worker, CancellationToken token)
    {
        var method = worker.GetType().GetMethod(
            "ExecuteAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsAssignableFrom<Task>(method.Invoke(worker, [token]));
    }

    private static void MarkAsRunToday(BackgroundService worker)
    {
        var field = worker.GetType().GetField(
            "_lastRunDate",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(worker, DateTime.UtcNow.Date);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _provider.Dispose();
        _connection.Dispose();
    }
}
