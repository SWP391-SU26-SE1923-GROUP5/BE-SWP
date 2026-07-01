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

    private void DebugWriteRawQuery()
    {
        // Helper used during development to dump raw rows for diagnostics.
        var conn = (Microsoft.Data.Sqlite.SqliteConnection)_dbContext.Database.GetDbConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT \"u_id\", \"create_at\" FROM StudyLogs ORDER BY \"create_at\" DESC";
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            Console.WriteLine($"LOG {rdr.GetGuid(0)} -> {rdr.GetValue(1)} (kind={rdr.GetFieldType(1).Name})");
        }
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

    [Fact]
    public async Task GetLeaderboardAsync_PeriodWeekly_AggregatesStudyLogXp()
    {
        // Arrange: two users with recent StudyLog activity + one user whose activity
        // is older than 7 days must NOT appear in the Weekly ranking.
        var activeUser = new User { Id = Guid.NewGuid(), FullName = "Active", Email = "active@test.com", PasswordHash = "hash" };
        var inactiveUser = new User { Id = Guid.NewGuid(), FullName = "Inactive", Email = "inactive@test.com", PasswordHash = "hash" };
        var staleUser = new User { Id = Guid.NewGuid(), FullName = "Stale", Email = "stale@test.com", PasswordHash = "hash" };
        _dbContext.Users.AddRange(activeUser, inactiveUser, staleUser);

        _dbContext.UserStats.AddRange(
            new UserStats { Id = Guid.NewGuid(), UserId = activeUser.Id, TotalXp = 1500, CurrentLevel = 5, CreatedAt = DateTime.UtcNow },
            new UserStats { Id = Guid.NewGuid(), UserId = inactiveUser.Id, TotalXp = 200, CurrentLevel = 2, CreatedAt = DateTime.UtcNow },
            new UserStats { Id = Guid.NewGuid(), UserId = staleUser.Id, TotalXp = 999, CurrentLevel = 3, CreatedAt = DateTime.UtcNow });
        
        await _dbContext.SaveChangesAsync();

        // Helper that bypasses ApplicationDbContext.ApplyAuditFields (which would otherwise
        // overwrite CreatedAt to UtcNow on every SaveChanges). We execute the insert
        // through a raw ADO.NET command so the historical timestamps survive intact.
        async Task SeedLogAsync(Guid userId, int xp, DateTime createdAt)
        {
            var conn = _dbContext.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO StudyLogs
                (log_id, u_id, activity_type, doc_id, subject_code, is_correct,
                 time_spent_seconds, xp_earned, create_at, update_at)
                VALUES ($id, $uid, $act, NULL, NULL, 0, NULL, $xp, $cat, NULL)";
            var pId = cmd.CreateParameter(); pId.ParameterName = "$id"; pId.Value = Guid.NewGuid(); cmd.Parameters.Add(pId);
            var pUid = cmd.CreateParameter(); pUid.ParameterName = "$uid"; pUid.Value = userId; cmd.Parameters.Add(pUid);
            var pAct = cmd.CreateParameter(); pAct.ParameterName = "$act"; pAct.Value = (int)ActivityType.FlashcardReview; cmd.Parameters.Add(pAct);
            var pXp = cmd.CreateParameter(); pXp.ParameterName = "$xp"; pXp.Value = xp; cmd.Parameters.Add(pXp);
            var pCat = cmd.CreateParameter(); pCat.ParameterName = "$cat"; pCat.Value = createdAt; cmd.Parameters.Add(pCat);
            await cmd.ExecuteNonQueryAsync();
        }

        var nowUtc = DateTime.UtcNow;
        await SeedLogAsync(activeUser.Id, 20, nowUtc.AddDays(-1));
        await SeedLogAsync(activeUser.Id, 20, nowUtc.AddDays(-3));
        await SeedLogAsync(activeUser.Id, 20, nowUtc.AddDays(-4));
        await SeedLogAsync(activeUser.Id, 500, nowUtc.AddDays(-10));
        await SeedLogAsync(inactiveUser.Id, 50, nowUtc.AddDays(-15));
        await SeedLogAsync(staleUser.Id, 999, nowUtc.AddDays(-30));

        // Act
        var result = await _gamificationService.GetLeaderboardAsync(10, LeaderboardPeriod.Weekly);

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Data!);

        var entry = result.Data![0];
        Assert.Equal(activeUser.Id, entry.UserId);
        Assert.Equal(60, entry.Xp);                              // Only the 3 recent logs counted
        Assert.Equal(1500, entry.TotalXp);                       // AllTime XP is preserved for display
        Assert.Equal(LeaderboardPeriod.Weekly, entry.Period);
        Assert.Equal(1, entry.Rank);

        // Stale + inactive users must NOT appear (no XP inside the rolling window).
        Assert.DoesNotContain(result.Data!, e => e.UserId == staleUser.Id);
        Assert.DoesNotContain(result.Data!, e => e.UserId == inactiveUser.Id);
    }

    [Fact]
    public async Task GetLeaderboardAsync_PeriodMonthly_AggregatesStudyLogXp()
    {
        // Arrange: two active users within the last 30 days + one outside the window.
        var top = new User { Id = Guid.NewGuid(), FullName = "Top", Email = "top@test.com", PasswordHash = "hash" };
        var second = new User { Id = Guid.NewGuid(), FullName = "Second", Email = "second@test.com", PasswordHash = "hash" };
        var outside = new User { Id = Guid.NewGuid(), FullName = "Outside", Email = "outside@test.com", PasswordHash = "hash" };
        _dbContext.Users.AddRange(top, second, outside);

        _dbContext.UserStats.AddRange(
            new UserStats { Id = Guid.NewGuid(), UserId = top.Id, TotalXp = 2000, CurrentLevel = 6, CreatedAt = DateTime.UtcNow },
            new UserStats { Id = Guid.NewGuid(), UserId = second.Id, TotalXp = 800, CurrentLevel = 4, CreatedAt = DateTime.UtcNow },
            new UserStats { Id = Guid.NewGuid(), UserId = outside.Id, TotalXp = 5000, CurrentLevel = 7, CreatedAt = DateTime.UtcNow });

        await _dbContext.SaveChangesAsync();

        // See the Weekly test for an explanation of why we use a raw SQL insert to seed
        // StudyLog rows (bypasses ApplyAuditFields which would overwrite CreatedAt).
        async Task SeedLogAsync(Guid userId, int xp, DateTime createdAt)
        {
            var conn = _dbContext.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO StudyLogs
                (log_id, u_id, activity_type, doc_id, subject_code, is_correct,
                 time_spent_seconds, xp_earned, create_at, update_at)
                VALUES ($id, $uid, $act, NULL, NULL, 0, NULL, $xp, $cat, NULL)";
            var pId = cmd.CreateParameter(); pId.ParameterName = "$id"; pId.Value = Guid.NewGuid(); cmd.Parameters.Add(pId);
            var pUid = cmd.CreateParameter(); pUid.ParameterName = "$uid"; pUid.Value = userId; cmd.Parameters.Add(pUid);
            var pAct = cmd.CreateParameter(); pAct.ParameterName = "$act"; pAct.Value = (int)ActivityType.FlashcardReview; cmd.Parameters.Add(pAct);
            var pXp = cmd.CreateParameter(); pXp.ParameterName = "$xp"; pXp.Value = xp; cmd.Parameters.Add(pXp);
            var pCat = cmd.CreateParameter(); pCat.ParameterName = "$cat"; pCat.Value = createdAt; cmd.Parameters.Add(pCat);
            await cmd.ExecuteNonQueryAsync();
        }

        var nowUtc = DateTime.UtcNow;
        await SeedLogAsync(top.Id, 100, nowUtc.AddDays(-15));
        await SeedLogAsync(top.Id, 100, nowUtc.AddDays(-25));
        await SeedLogAsync(top.Id, 100, nowUtc.AddDays(-40));
        await SeedLogAsync(second.Id, 40, nowUtc.AddDays(-10));
        await SeedLogAsync(second.Id, 40, nowUtc.AddDays(-20));
        await SeedLogAsync(outside.Id, 500, nowUtc.AddDays(-45));

        // Act
        var result = await _gamificationService.GetLeaderboardAsync(10, LeaderboardPeriod.Monthly);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count);

        Assert.Equal(top.Id, result.Data[0].UserId);
        Assert.Equal(200, result.Data[0].Xp);
        Assert.Equal(2000, result.Data[0].TotalXp);
        Assert.Equal(1, result.Data[0].Rank);

        Assert.Equal(second.Id, result.Data[1].UserId);
        Assert.Equal(80, result.Data[1].Xp);
        Assert.Equal(800, result.Data[1].TotalXp);
        Assert.Equal(2, result.Data[1].Rank);

        Assert.All(result.Data!, e => Assert.Equal(LeaderboardPeriod.Monthly, e.Period));
        Assert.DoesNotContain(result.Data!, e => e.UserId == outside.Id);
    }

    [Fact]
    public async Task GetLeaderboardAsync_PeriodAllTime_UsesUserStatsTotalXp()
    {
        // Arrange: AllTime ignores StudyLog entirely and sorts by UserStats.TotalXp.
        var a = new User { Id = Guid.NewGuid(), FullName = "Alpha", Email = "alpha@test.com", PasswordHash = "hash" };
        var b = new User { Id = Guid.NewGuid(), FullName = "Beta", Email = "beta@test.com", PasswordHash = "hash" };
        _dbContext.Users.AddRange(a, b);

        _dbContext.UserStats.AddRange(
            new UserStats { Id = Guid.NewGuid(), UserId = a.Id, TotalXp = 400, CurrentLevel = 3, CreatedAt = DateTime.UtcNow },
            new UserStats { Id = Guid.NewGuid(), UserId = b.Id, TotalXp = 900, CurrentLevel = 5, CreatedAt = DateTime.UtcNow });

        // Some StudyLogs - they MUST NOT affect the AllTime ranking.
        _dbContext.StudyLogs.AddRange(
            new StudyLog { Id = Guid.NewGuid(), UserId = a.Id, XpEarned = 9999, ActivityType = ActivityType.FlashcardReview, CreatedAt = DateTime.UtcNow.AddDays(-1) });

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _gamificationService.GetLeaderboardAsync(10, LeaderboardPeriod.AllTime);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count);

        Assert.Equal(b.Id, result.Data[0].UserId);
        Assert.Equal(900, result.Data[0].TotalXp);
        Assert.Equal(900, result.Data[0].Xp);                   // For AllTime, Xp == TotalXp
        Assert.Equal(LeaderboardPeriod.AllTime, result.Data[0].Period);
        Assert.Equal(1, result.Data[0].Rank);

        Assert.Equal(a.Id, result.Data[1].UserId);
        Assert.Equal(400, result.Data[1].TotalXp);
        Assert.Equal(400, result.Data[1].Xp);
        Assert.Equal(2, result.Data[1].Rank);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
        _connection.Dispose();
        _unitOfWork.Dispose();
    }
}
