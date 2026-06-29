using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AIStudyHub.Business.DTOs.Gamification;
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

public class GamificationServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _dbContext;
    private readonly UnitOfWork _unitOfWork;
    private readonly Mock<ILogger<GamificationService>> _loggerMock;
    private readonly GamificationService _gamificationService;

    public GamificationServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        _dbContext = new ApplicationDbContext(options);
        _dbContext.Database.EnsureCreated();

        _unitOfWork = new UnitOfWork(_dbContext);
        _loggerMock = new Mock<ILogger<GamificationService>>();

        _gamificationService = new GamificationService(_unitOfWork, _loggerMock.Object);
    }

    [Fact]
    public async Task GetStatsAsync_UserStatsMissing_CreatesNewStats()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var result = await _gamificationService.GetStatsAsync(userId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.Data!.TotalXp);
        Assert.Equal(1, result.Data.CurrentLevel);
        
        var dbStats = await _dbContext.UserStats.FirstOrDefaultAsync(s => s.UserId == userId);
        Assert.NotNull(dbStats);
        Assert.Equal(0, dbStats.TotalXp);
    }

    [Fact]
    public async Task AwardXpAsync_CorrectFlashcard_Awards10Xp()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new XpAwardRequest(userId, 0, true, ActivityType.FlashcardReview, null, null, null);

        // Act
        var result = await _gamificationService.AwardXpAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(10, result.Data!.XpEarned);
        Assert.Equal(10, result.Data.TotalXp);
        Assert.Equal(1, result.Data.CurrentStreak);
        
        var logs = await _dbContext.StudyLogs.ToListAsync();
        Assert.Single(logs);
        Assert.Equal(10, logs[0].XpEarned);
    }

    [Fact]
    public async Task AwardXpAsync_LevelUp_CalculatesLevelCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        // Give the user 95 XP initially
        _dbContext.UserStats.Add(new UserStats { Id = Guid.NewGuid(), UserId = userId, TotalXp = 95, CurrentLevel = 1, CreatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var request = new XpAwardRequest(userId, 0, true, ActivityType.FlashcardReview, null, null, null); // Awards 10 XP

        // Act
        var result = await _gamificationService.AwardXpAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(105, result.Data!.TotalXp);
        Assert.Equal(2, result.Data.NewLevel); // 100 XP is threshold for Level 2
        Assert.True(result.Data.LeveledUp);
    }

    [Fact]
    public async Task GetLeaderboardAsync_ReturnsTopUsers()
    {
        // Arrange
        var user1 = new User { Id = Guid.NewGuid(), FullName = "User 1", Email = "u1@test.com", PasswordHash = "hash" };
        var user2 = new User { Id = Guid.NewGuid(), FullName = "User 2", Email = "u2@test.com", PasswordHash = "hash" };
        _dbContext.Users.AddRange(user1, user2);
        
        _dbContext.UserStats.Add(new UserStats { Id = Guid.NewGuid(), UserId = user1.Id, TotalXp = 500, CurrentLevel = 4, CreatedAt = DateTime.UtcNow });
        _dbContext.UserStats.Add(new UserStats { Id = Guid.NewGuid(), UserId = user2.Id, TotalXp = 1000, CurrentLevel = 5, CreatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _gamificationService.GetLeaderboardAsync(10);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count);
        Assert.Equal(user2.Id, result.Data[0].UserId); // User 2 should be first because of higher XP
        Assert.Equal(1, result.Data[0].Rank);
        Assert.Equal(2, result.Data[1].Rank);
    }

    [Fact]
    public async Task AwardXpAsync_WithPositiveTimeSpentSeconds_AccumulatesTotalStudySeconds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new XpAwardRequest(userId, 0, true, ActivityType.FlashcardReview, null, null, TimeSpentSeconds: 120);

        // Act
        var result = await _gamificationService.AwardXpAsync(request);

        // Assert
        Assert.True(result.Success);
        var stats = await _dbContext.UserStats.SingleAsync(s => s.UserId == userId);
        Assert.Equal(120, stats.TotalStudySeconds);
        Assert.Equal(120, result.Data!.TotalStudySeconds);

        var log = await _dbContext.StudyLogs.SingleAsync();
        Assert.Equal(120, log.TimeSpentSeconds);
    }

    [Fact]
    public async Task AwardXpAsync_WithMultipleReviews_AccumulatesTotalStudySecondsAdditively()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var first = new XpAwardRequest(userId, 0, true, ActivityType.FlashcardReview, null, null, TimeSpentSeconds: 60);
        var second = new XpAwardRequest(userId, 0, false, ActivityType.FlashcardReview, null, null, TimeSpentSeconds: 30);

        // Act
        await _gamificationService.AwardXpAsync(first);
        await _gamificationService.AwardXpAsync(second);

        // Assert
        var stats = await _dbContext.UserStats.SingleAsync(s => s.UserId == userId);
        Assert.Equal(90, stats.TotalStudySeconds);
    }

    [Fact]
    public async Task AwardXpAsync_WithNullOrNonPositiveTimeSpentSeconds_DoesNotAccumulate()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new XpAwardRequest(userId, 0, true, ActivityType.FlashcardReview, null, null, TimeSpentSeconds: null);

        // Act
        var result = await _gamificationService.AwardXpAsync(request);

        // Assert
        Assert.True(result.Success);
        var stats = await _dbContext.UserStats.SingleAsync(s => s.UserId == userId);
        Assert.Equal(0, stats.TotalStudySeconds);

        // Negative values (e.g. malformed client) must not corrupt the column
        var negativeRequest = new XpAwardRequest(userId, 0, true, ActivityType.FlashcardReview, null, null, TimeSpentSeconds: -5);
        await _gamificationService.AwardXpAsync(negativeRequest);
        stats = await _dbContext.UserStats.SingleAsync(s => s.UserId == userId);
        Assert.Equal(0, stats.TotalStudySeconds);
    }

    [Fact]
    public async Task AwardXpAsync_TimeSpentSecondsZero_DoesNotAccumulate()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new XpAwardRequest(userId, 0, true, ActivityType.FlashcardReview, null, null, TimeSpentSeconds: 0);

        // Act
        var result = await _gamificationService.AwardXpAsync(request);

        // Assert
        Assert.True(result.Success);
        var stats = await _dbContext.UserStats.SingleAsync(s => s.UserId == userId);
        Assert.Equal(0, stats.TotalStudySeconds);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
        _connection.Dispose();
        _unitOfWork.Dispose();
    }
}
