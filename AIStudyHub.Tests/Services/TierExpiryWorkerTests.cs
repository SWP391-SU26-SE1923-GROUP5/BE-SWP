using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AIStudyHub.Business.DTOs.Gamification;
using AIStudyHub.Business.DTOs.Notifications;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Workers;
using AIStudyHub.Data;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;
using AIStudyHub.Data.Interfaces;
using AIStudyHub.Data.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AIStudyHub.Tests.Services;

public class TierExpiryWorkerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _dbContext;
    private readonly UnitOfWork _unitOfWork;
    private readonly Mock<IRealTimeNotificationService> _notifierMock;
    private readonly TierExpiryWorker _worker;

    public TierExpiryWorkerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        _dbContext = new ApplicationDbContext(options);
        _dbContext.Database.EnsureCreated();

        _unitOfWork = new UnitOfWork(_dbContext);
        _notifierMock = new Mock<IRealTimeNotificationService>();

        // Build a minimal DI container so the worker can resolve scopes itself.
        var services = new ServiceCollection();
        services.AddSingleton<IUnitOfWork>(_unitOfWork);
        services.AddSingleton(_notifierMock.Object);
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        _worker = new TierExpiryWorker(
            scopeFactory,
            Mock.Of<ILogger<TierExpiryWorker>>());
    }

    [Fact]
    public async Task RunOnceAsync_UserExpiresIn3Days_CreatesNotificationAndBroadcasts()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tierId = Guid.NewGuid();
        _dbContext.TierMemberships.Add(new TierMembership
        {
            Id = tierId,
            TierName = "Pro",
            Price = 99000m,
            StorageLimitMb = 1000,
            AiTokens = 100000,
            CreatedAt = DateTime.UtcNow
        });
        _dbContext.Users.Add(new User
        {
            Id = userId,
            FullName = "U",
            Email = "u@test.com",
            PasswordHash = "h",
            TierId = tierId,
            TierExpireAt = DateTime.UtcNow.Date.AddDays(3),
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var sent = await _worker.RunOnceAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, sent);
        var note = _dbContext.Notifications.Single(n => n.UserId == userId);
        Assert.Equal(NotificationType.TierExpired, note.Type);
        Assert.Contains("Pro", note.Message);
        _notifierMock.Verify(n => n.NotifyTierExpiringSoonAsync(
            userId, "Pro", It.IsAny<DateTime>(), 3, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_UserExpiresIn10Days_NotNotified()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _dbContext.Users.Add(new User
        {
            Id = userId,
            FullName = "U",
            Email = "u@test.com",
            PasswordHash = "h",
            TierId = Guid.NewGuid(),
            TierExpireAt = DateTime.UtcNow.Date.AddDays(10),
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var sent = await _worker.RunOnceAsync(CancellationToken.None);

        // Assert
        Assert.Equal(0, sent);
        Assert.Empty(_dbContext.Notifications);
    }

    [Fact]
    public async Task RunOnceAsync_AlreadyNotifiedToday_IsIdempotent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tierId = Guid.NewGuid();
        _dbContext.TierMemberships.Add(new TierMembership
        {
            Id = tierId,
            TierName = "Pro",
            Price = 0,
            StorageLimitMb = 100,
            AiTokens = 0,
            CreatedAt = DateTime.UtcNow
        });
        _dbContext.Users.Add(new User
        {
            Id = userId,
            FullName = "U",
            Email = "u@test.com",
            PasswordHash = "h",
            TierId = tierId,
            TierExpireAt = DateTime.UtcNow.Date.AddDays(3),
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // First tick creates the notification
        var first = await _worker.RunOnceAsync(CancellationToken.None);
        Assert.Equal(1, first);

        // Second tick should NOT create a duplicate
        var second = await _worker.RunOnceAsync(CancellationToken.None);
        Assert.Equal(0, second);

        var notes = _dbContext.Notifications.Where(n => n.UserId == userId).ToList();
        Assert.Single(notes);
        _notifierMock.Verify(n => n.NotifyTierExpiringSoonAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_UserExpiresIn1Day_CreatesNotification()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tierId = Guid.NewGuid();
        _dbContext.TierMemberships.Add(new TierMembership
        {
            Id = tierId,
            TierName = "Elite",
            Price = 199000m,
            StorageLimitMb = 5000,
            AiTokens = 500000,
            CreatedAt = DateTime.UtcNow
        });
        _dbContext.Users.Add(new User
        {
            Id = userId,
            FullName = "U",
            Email = "u@test.com",
            PasswordHash = "h",
            TierId = tierId,
            TierExpireAt = DateTime.UtcNow.Date.AddDays(1),
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var sent = await _worker.RunOnceAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, sent);
        _notifierMock.Verify(n => n.NotifyTierExpiringSoonAsync(
            userId, "Elite", It.IsAny<DateTime>(), 1, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
        _connection.Dispose();
        _unitOfWork.Dispose();
    }
}