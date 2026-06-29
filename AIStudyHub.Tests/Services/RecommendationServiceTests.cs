using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AIStudyHub.Business.DTOs.Recommendations;
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

public class RecommendationServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _dbContext;
    private readonly UnitOfWork _unitOfWork;
    private readonly Mock<ILogger<RecommendationService>> _loggerMock;
    private readonly RecommendationService _service;

    public RecommendationServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        _dbContext = new ApplicationDbContext(options);
        _dbContext.Database.EnsureCreated();

        _unitOfWork = new UnitOfWork(_dbContext);
        _loggerMock = new Mock<ILogger<RecommendationService>>();

        _service = new RecommendationService(_unitOfWork, _loggerMock.Object);
    }

    [Fact]
    public async Task GetSubjectMasteryAsync_ComputesCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _dbContext.Subjects.Add(new Subject { SubjectCode = "TEST-SWP", SubjectName = "Software Development Project" });
        _dbContext.Subjects.Add(new Subject { SubjectCode = "TEST-PRN", SubjectName = "C# Programming" });

        // SWP391: 2 correct, 1 incorrect => 66.67%
        _dbContext.StudyLogs.Add(new StudyLog { Id = Guid.NewGuid(), UserId = userId, SubjectCode = "TEST-SWP", ActivityType = ActivityType.FlashcardReview, IsCorrect = true, CreatedAt = DateTime.UtcNow });
        _dbContext.StudyLogs.Add(new StudyLog { Id = Guid.NewGuid(), UserId = userId, SubjectCode = "TEST-SWP", ActivityType = ActivityType.FlashcardReview, IsCorrect = true, CreatedAt = DateTime.UtcNow });
        _dbContext.StudyLogs.Add(new StudyLog { Id = Guid.NewGuid(), UserId = userId, SubjectCode = "TEST-SWP", ActivityType = ActivityType.QuizSubmission, IsCorrect = false, CreatedAt = DateTime.UtcNow });

        // PRN211: 1 correct, 0 incorrect => 100%
        _dbContext.StudyLogs.Add(new StudyLog { Id = Guid.NewGuid(), UserId = userId, SubjectCode = "TEST-PRN", ActivityType = ActivityType.QuizSubmission, IsCorrect = true, CreatedAt = DateTime.UtcNow });

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetSubjectMasteryAsync(userId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count);

        var swp = result.Data.First(m => m.SubjectCode == "TEST-SWP");
        Assert.Equal(3, swp.TotalAttempts);
        Assert.Equal(2, swp.CorrectAttempts);
        Assert.Equal(66.67, swp.MasteryPercentage);
        Assert.Equal("Software Development Project", swp.SubjectName);

        var prn = result.Data.First(m => m.SubjectCode == "TEST-PRN");
        Assert.Equal(1, prn.TotalAttempts);
        Assert.Equal(1, prn.CorrectAttempts);
        Assert.Equal(100.0, prn.MasteryPercentage);
    }

    [Fact]
    public async Task GetRecommendationsAsync_NoActivity_ReturnsDefaultMessage()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var result = await _service.GetRecommendationsAsync(userId);

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.Data!.SubjectMasteries);
        Assert.Contains("Start by submitting a quiz", result.Data.Recommendations.First());
    }

    [Fact]
    public async Task GetRecommendationsAsync_WithActivity_ReturnsRecommendations()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _dbContext.Subjects.Add(new Subject { SubjectCode = "TEST-MATH", SubjectName = "Mathematics" });
        // Weak: 1/3 correct = 33.3%
        _dbContext.StudyLogs.Add(new StudyLog { Id = Guid.NewGuid(), UserId = userId, SubjectCode = "TEST-MATH", ActivityType = ActivityType.QuizSubmission, IsCorrect = true, CreatedAt = DateTime.UtcNow });
        _dbContext.StudyLogs.Add(new StudyLog { Id = Guid.NewGuid(), UserId = userId, SubjectCode = "TEST-MATH", ActivityType = ActivityType.QuizSubmission, IsCorrect = false, CreatedAt = DateTime.UtcNow });
        _dbContext.StudyLogs.Add(new StudyLog { Id = Guid.NewGuid(), UserId = userId, SubjectCode = "TEST-MATH", ActivityType = ActivityType.QuizSubmission, IsCorrect = false, CreatedAt = DateTime.UtcNow });
        
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetRecommendationsAsync(userId);

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Data!.SubjectMasteries);
        Assert.Contains("Focus on Mathematics", result.Data.Recommendations.First());
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
        _connection.Dispose();
        _unitOfWork.Dispose();
    }
}
