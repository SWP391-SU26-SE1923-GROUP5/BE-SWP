using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AIStudyHub.Business.DTOs.Subscriptions;
using AIStudyHub.Business.Services;
using AIStudyHub.Data;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;
using AIStudyHub.Data.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AIStudyHub.Tests.Services;

public class SubscriptionServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _dbContext;
    private readonly UnitOfWork _unitOfWork;
    private readonly SubscriptionService _service;

    public SubscriptionServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        _dbContext = new ApplicationDbContext(options);
        _dbContext.Database.EnsureCreated();

        _unitOfWork = new UnitOfWork(_dbContext);
        _service = new SubscriptionService(_unitOfWork, Mock.Of<ILogger<SubscriptionService>>());
    }

    [Fact]
    public async Task GetMySubscriptionAsync_ActivePlanNotExpiringSoon_ReportsDaysRemaining()
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
            TierExpireAt = DateTime.UtcNow.Date.AddDays(10),
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetMySubscriptionAsync(userId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Pro", result.Data!.Current.TierName);
        Assert.False(result.Data.Current.IsExpiringSoon);
        Assert.False(result.Data.Current.IsExpired);
        Assert.InRange(result.Data.Current.DaysRemaining!.Value, 9, 11);
    }

    [Fact]
    public async Task GetMySubscriptionAsync_PlanExpiresIn3Days_IsExpiringSoon()
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

        // Act
        var result = await _service.GetMySubscriptionAsync(userId);

        // Assert
        Assert.True(result.Data!.Current.IsExpiringSoon);
        Assert.False(result.Data.Current.IsExpired);
    }

    [Fact]
    public async Task GetMySubscriptionAsync_AlreadyExpired_ReportsNegativeDays()
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
            TierExpireAt = DateTime.UtcNow.Date.AddDays(-2),
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetMySubscriptionAsync(userId);

        // Assert
        Assert.True(result.Data!.Current.IsExpired);
        Assert.False(result.Data.Current.IsExpiringSoon);
        Assert.True(result.Data.Current.DaysRemaining < 0);
    }

    [Fact]
    public async Task GetMySubscriptionAsync_WithCompletedPayments_AppearsInHistory()
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
            TierExpireAt = DateTime.UtcNow.Date.AddDays(30),
            CreatedAt = DateTime.UtcNow
        });
        _dbContext.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TierId = tierId,
            Amount = 99000m,
            PaymentDate = DateTime.UtcNow.AddDays(-30),
            Status = PaymentStatus.Completed,
            TransactionId = "T1",
            CreatedAt = DateTime.UtcNow
        });
        _dbContext.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TierId = tierId,
            Amount = 99000m,
            PaymentDate = DateTime.UtcNow,
            Status = PaymentStatus.Pending, // shouldn't appear
            TransactionId = "T2",
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetMySubscriptionAsync(userId);

        // Assert
        Assert.Single(result.Data!.History);
        Assert.Equal("Pro", result.Data.History[0].TierName);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
        _connection.Dispose();
        _unitOfWork.Dispose();
    }
}