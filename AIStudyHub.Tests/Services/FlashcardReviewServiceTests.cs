using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AIStudyHub.Business.DTOs.Common;
using AIStudyHub.Business.DTOs.FlashcardReviews;
using AIStudyHub.Business.DTOs.Gamification;
using AIStudyHub.Business.Interfaces.Services;
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

public class FlashcardReviewServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _dbContext;
    private readonly UnitOfWork _unitOfWork;
    private readonly Mock<ILogger<FlashcardReviewService>> _loggerMock;
    private readonly FlashcardReviewService _service;

    public FlashcardReviewServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        _dbContext = new ApplicationDbContext(options);
        _dbContext.Database.EnsureCreated();

        _unitOfWork = new UnitOfWork(_dbContext);
        _loggerMock = new Mock<ILogger<FlashcardReviewService>>();

        _service = new FlashcardReviewService(_unitOfWork, _loggerMock.Object);
    }

    [Fact]
    public async Task ProcessReviewAsync_NewReview_CreatesAndAppliesSm2()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var flashcardId = Guid.NewGuid();

        _dbContext.Flashcards.Add(new Flashcard { Id = flashcardId, Front = "A", Back = "B", DocumentId = Guid.NewGuid() });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.ProcessReviewAsync(userId, flashcardId, ReviewQuality.Easy);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.Data!.Review.Repetitions); // Because Easy increments reps to 1
        Assert.Equal(1, result.Data.Review.Interval); // Interval becomes 1 on first easy review

        var dbReview = await _dbContext.FlashcardReviews.FirstOrDefaultAsync(r => r.FlashcardId == flashcardId && r.UserId == userId);
        Assert.NotNull(dbReview);
        Assert.Equal(1, dbReview.Repetitions);
    }

    [Fact]
    public async Task ProcessReviewAsync_ExistingReview_UpdatesSm2()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var flashcardId = Guid.NewGuid();

        _dbContext.Flashcards.Add(new Flashcard { Id = flashcardId, Front = "A", Back = "B", DocumentId = Guid.NewGuid() });
        _dbContext.FlashcardReviews.Add(new FlashcardReview
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FlashcardId = flashcardId,
            EaseFactor = 2.5f,
            Interval = 1,
            Repetitions = 1,
            NextReviewDate = DateTime.UtcNow.AddDays(-1) // Overdue
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.ProcessReviewAsync(userId, flashcardId, ReviewQuality.Easy);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Review.Repetitions);
        Assert.Equal(6, result.Data.Review.Interval); // SM-2 rules: 2nd repetition interval is 6
    }

    [Fact]
    public async Task GetDueAsync_ReturnsOnlyDueFlashcards()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var flashcardId1 = Guid.NewGuid();
        var flashcardId2 = Guid.NewGuid();

        _dbContext.Flashcards.Add(new Flashcard { Id = flashcardId1, Front = "A", Back = "B", DocumentId = Guid.NewGuid() });
        _dbContext.Flashcards.Add(new Flashcard { Id = flashcardId2, Front = "C", Back = "D", DocumentId = Guid.NewGuid() });
        
        // Due review
        _dbContext.FlashcardReviews.Add(new FlashcardReview { Id = Guid.NewGuid(), UserId = userId, FlashcardId = flashcardId1, NextReviewDate = DateTime.UtcNow.AddDays(-1) });
        // Future review
        _dbContext.FlashcardReviews.Add(new FlashcardReview { Id = Guid.NewGuid(), UserId = userId, FlashcardId = flashcardId2, NextReviewDate = DateTime.UtcNow.AddDays(1) });
        
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetDueAsync(userId, 10);

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal(flashcardId1, result.Data![0].FlashcardId);
    }

    [Fact]
    public async Task GetStatsAsync_ReturnsCorrectStats()
    {
        // Arrange
        var userId = Guid.NewGuid();
        
        // Mastered and Due
        _dbContext.FlashcardReviews.Add(new FlashcardReview { Id = Guid.NewGuid(), UserId = userId, FlashcardId = Guid.NewGuid(), EaseFactor = 2.5f, Interval = 21, NextReviewDate = DateTime.UtcNow.AddDays(-1) });
        // Not Mastered, Future
        _dbContext.FlashcardReviews.Add(new FlashcardReview { Id = Guid.NewGuid(), UserId = userId, FlashcardId = Guid.NewGuid(), EaseFactor = 2.0f, Interval = 5, NextReviewDate = DateTime.UtcNow.AddDays(1) });
        
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetStatsAsync(userId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.TotalReviewed);
        Assert.Equal(1, result.Data.DueNow);
        Assert.Equal(1, result.Data.MasteredCount);
        Assert.Equal(2.25f, result.Data.AverageEaseFactor, 2);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
        _connection.Dispose();
        _unitOfWork.Dispose();
    }

    [Fact]
    public async Task ProcessReviewAsync_ForwardsTimeSpentSecondsToGamificationService()
    {
        // Arrange - construct a service that wires a mocked gamification
        var userId = Guid.NewGuid();
        var flashcardId = Guid.NewGuid();
        _dbContext.Flashcards.Add(new Flashcard { Id = flashcardId, Front = "Q", Back = "A", DocumentId = Guid.NewGuid() });
        await _dbContext.SaveChangesAsync();

        var gamificationMock = new Mock<IGamificationService>();
        gamificationMock
            .Setup(g => g.AwardXpAsync(It.IsAny<XpAwardRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<XpAwardResult>.Ok(new XpAwardResult(10, 10, 1, 1, false, 1, 1, 0)));

        var serviceWithGamification = new FlashcardReviewService(_unitOfWork, _loggerMock.Object, gamificationMock.Object);

        // Act
        await serviceWithGamification.ProcessReviewAsync(userId, flashcardId, ReviewQuality.Easy, timeSpentSeconds: 240);

        // Assert
        gamificationMock.Verify(
            g => g.AwardXpAsync(
                It.Is<XpAwardRequest>(r => r.UserId == userId && r.TimeSpentSeconds == 240 && r.ActivityType == ActivityType.FlashcardReview),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessReviewAsync_WithoutGamificationService_DoesNotThrow()
    {
        // Arrange - constructor without gamification must remain backward-compatible
        var userId = Guid.NewGuid();
        var flashcardId = Guid.NewGuid();
        _dbContext.Flashcards.Add(new Flashcard { Id = flashcardId, Front = "Q", Back = "A", DocumentId = Guid.NewGuid() });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.ProcessReviewAsync(userId, flashcardId, ReviewQuality.Easy, timeSpentSeconds: 30);

        // Assert
        Assert.True(result.Success);
    }
}
