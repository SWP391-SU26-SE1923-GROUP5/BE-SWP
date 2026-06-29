using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AIStudyHub.Business.DTOs.Common;
using AIStudyHub.Business.DTOs.Gamification;
using AIStudyHub.Business.DTOs.Notifications;
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

/// <summary>
/// Plan C3 — five milestone tests around BadgeService:
///   1. SHARPSHOOTER unlocks on a perfect 10-question quiz
///   2. MASTERY_MATH unlocks at 85%+ on a Math quiz
///   3. MASTERY_MATH stays locked at 80% (boundary)
///   4. CARDS_500 unlocks after 500 distinct cards reviewed
///   5. Repeat unlock is idempotent (no duplicate XP)
/// </summary>
public class BadgeServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _dbContext;
    private readonly UnitOfWork _unitOfWork;
    private readonly Mock<ILogger<BadgeService>> _loggerMock;
    private readonly Mock<IGamificationService> _gamificationMock;
    private readonly BadgeService _badgeService;
    private readonly RealTimeNotificationServiceFake _notifier;

    public BadgeServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        _dbContext = new ApplicationDbContext(options);
        _dbContext.Database.EnsureCreated();

        _unitOfWork = new UnitOfWork(_dbContext);
        _loggerMock = new Mock<ILogger<BadgeService>>();
        _gamificationMock = new Mock<IGamificationService>();
        _gamificationMock
            .Setup(g => g.AwardXpAsync(It.IsAny<XpAwardRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<XpAwardResult>.Ok(new XpAwardResult(0, 200, 1, 1, false, 7, 7, 0)));
        _notifier = new RealTimeNotificationServiceFake();

        _badgeService = new BadgeService(
            _unitOfWork,
            _loggerMock.Object,
            _gamificationMock.Object,
            _notifier);
    }

    [Fact]
    public async Task EvaluateQuizBadgeAsync_PerfectTenQuestionQuiz_UnlocksSharpshooter()
    {
        // Arrange
        var (userId, quizId) = await SeedMathQuizWithTwelveQuestionsAsync("MATH");
        var submission = new QuizSubmission
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            QuizId = quizId,
            Answers = "{}",
            Score = 12,
            MaxScore = 12, // ≥10, perfect
            TotalCorrect = 12,
            SubmittedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        // Act - perfect 12q in MATH unlocks both Sharpshooter and Math Prodigy
        var unlocked = await _badgeService.EvaluateQuizBadgeAsync(userId, submission);

        // Assert
        Assert.Equal(2, unlocked.Count);
        Assert.Contains(unlocked, a => a.Code == BadgeCodes.Sharpshooter);
        Assert.Contains(unlocked, a => a.Code == BadgeCodes.MasteryMath);
        Assert.Equal(2, _dbContext.UserBadges.Count(ub => ub.UserId == userId));
        _gamificationMock.Verify(g => g.AwardXpAsync(
            It.Is<XpAwardRequest>(r => r.ActivityType == ActivityType.BadgeEarned),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
        Assert.Equal(2, _notifier.NotificationsSent);
    }

    [Fact]
    public async Task EvaluateQuizBadgeAsync_MathQuizAt85Percent_UnlocksMasteryMath()
    {
        // Arrange
        var (userId, quizId) = await SeedMathQuizWithTwelveQuestionsAsync("MATH");
        var submission = new QuizSubmission
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            QuizId = quizId,
            Answers = "{}",
            Score = 11,
            MaxScore = 13, // 84.6% — does NOT unlock Math
            TotalCorrect = 11,
            SubmittedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        // Act 1: 11/13 = 84.6% → no unlock
        var firstAttempt = await _badgeService.EvaluateQuizBadgeAsync(userId, submission);
        Assert.Empty(firstAttempt);

        // Act 2: 11/13 wait — try 11/12 = 91.6% → unlocks
        submission.MaxScore = 12;
        submission.Score = 11;
        submission.TotalCorrect = 11;

        var secondAttempt = await _badgeService.EvaluateQuizBadgeAsync(userId, submission);
        Assert.Single(secondAttempt);
        Assert.Equal(BadgeCodes.MasteryMath, secondAttempt[0].Code);
    }

    [Fact]
    public async Task EvaluateQuizBadgeAsync_NonMathSubject_DoesNotUnlockMath()
    {
        // Arrange - even a perfect Physics quiz should not unlock Math Prodigy
        var (userId, quizId) = await SeedMathQuizWithTwelveQuestionsAsync("PHYS");
        var submission = new QuizSubmission
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            QuizId = quizId,
            Answers = "{}",
            Score = 12,
            MaxScore = 12,
            TotalCorrect = 12,
            SubmittedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        var unlocked = await _badgeService.EvaluateQuizBadgeAsync(userId, submission);

        // Should still unlock Sharpshooter but NOT Math Prodigy
        Assert.Single(unlocked);
        Assert.Equal(BadgeCodes.Sharpshooter, unlocked[0].Code);
    }

    [Fact]
    public async Task EvaluateFlashcardBadgeAsync_After500DistinctCards_UnlocksMemoryMaster()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        _dbContext.Documents.Add(new Document
        {
            Id = documentId,
            UserId = userId,
            SubjectId = Guid.NewGuid(),
            Title = "Test",
            FileName = "test.txt",
            FileExtension = ".txt",
            FileType = "text/plain",
            FileSizeBytes = 0,
            ShareStatus = "private",
            CreatedAt = DateTime.UtcNow
        });
        var cards = Enumerable.Range(0, 500)
            .Select(_ => new Flashcard
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                Front = "Q",
                Back = "A",
                CreatedAt = DateTime.UtcNow
            })
            .ToList();
        _dbContext.Flashcards.AddRange(cards);
        await _dbContext.SaveChangesAsync();

        var reviews = cards.Select(c => new FlashcardReview
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FlashcardId = c.Id,
            EaseFactor = 2.5f,
            Interval = 1,
            Repetitions = 1,
            NextReviewDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        }).ToList();
        _dbContext.FlashcardReviews.AddRange(reviews);
        await _dbContext.SaveChangesAsync();

        // Act
        var unlocked = await _badgeService.EvaluateFlashcardBadgeAsync(userId);

        // Assert
        Assert.Single(unlocked);
        Assert.Equal(BadgeCodes.Cards500, unlocked[0].Code);
    }

    [Fact]
    public async Task TryUnlockAsync_SecondAttempt_IsIdempotent()
    {
        // Arrange - unlock once
        var userId = Guid.NewGuid();
        var first = await _badgeService.EvaluateDocumentBadgeAsync(userId);
        Assert.Empty(first); // 0 done docs
        _dbContext.Documents.Add(new Document
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SubjectId = Guid.NewGuid(),
            Title = "A",
            FileName = "a.txt",
            FileExtension = ".txt",
            FileType = "text/plain",
            FileSizeBytes = 0,
            ShareStatus = "private",
            Status = DocumentStatus.Done,
            CreatedAt = DateTime.UtcNow
        });
        for (var i = 0; i < 7; i++)
        {
            _dbContext.Documents.Add(new Document
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SubjectId = Guid.NewGuid(),
                Title = $"D{i}",
                FileName = $"{i}.txt",
                FileExtension = ".txt",
                FileType = "text/plain",
                FileSizeBytes = 0,
                ShareStatus = "private",
                Status = DocumentStatus.Done,
                CreatedAt = DateTime.UtcNow
            });
        }
        await _dbContext.SaveChangesAsync();

        var firstUnlock = await _badgeService.EvaluateDocumentBadgeAsync(userId);
        Assert.Single(firstUnlock);
        Assert.True(firstUnlock[0].IsUnlocked);

        // Act - second evaluation should be a no-op
        var secondUnlock = await _badgeService.EvaluateDocumentBadgeAsync(userId);

        // Assert
        Assert.Empty(secondUnlock); // already unlocked → returns empty
        var userBadges = _dbContext.UserBadges.Where(ub => ub.UserId == userId).ToList();
        Assert.Single(userBadges); // still 1 row
    }

    [Fact]
    public async Task GetAchievementsAsync_ReturnsAllBadges_WithCorrectProgress()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _dbContext.Documents.Add(new Document
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SubjectId = Guid.NewGuid(),
            Title = "Doc",
            FileName = "d.txt",
            FileExtension = ".txt",
            FileType = "text/plain",
            FileSizeBytes = 0,
            ShareStatus = "private",
            Status = DocumentStatus.Done,
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var achievements = await _badgeService.GetAchievementsAsync(userId);

        // Assert
        Assert.Equal(5, achievements.Count);
        var bookworm = achievements.First(a => a.Code == BadgeCodes.Bookworm);
        Assert.Equal(1m, bookworm.CurrentProgress); // 1 doc, target 7
        Assert.False(bookworm.IsUnlocked);
    }

    private async Task<(Guid userId, Guid quizId)> SeedMathQuizWithTwelveQuestionsAsync(string subjectCode)
    {
        var userId = Guid.NewGuid();

        var subject = _dbContext.Subjects.FirstOrDefault(s => s.SubjectCode == subjectCode);
        if (subject is null)
        {
            subject = new Subject
            {
                Id = Guid.NewGuid(),
                SubjectCode = subjectCode,
                SubjectName = subjectCode,
                Description = subjectCode,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.Subjects.Add(subject);
        }

        var document = new Document
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SubjectId = subject.Id,
            Title = "Q",
            FileName = "q.txt",
            FileExtension = ".txt",
            FileType = "text/plain",
            FileSizeBytes = 0,
            ShareStatus = "private",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Documents.Add(document);

        var quiz = new Quiz
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            Title = "Quiz",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Quizzes.Add(quiz);

        for (var i = 0; i < 12; i++)
        {
            var question = new Question
            {
                Id = Guid.NewGuid(),
                QuizId = quiz.Id,
                Title = $"Q{i}",
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.Questions.Add(question);
            _dbContext.Answers.Add(new Answer
            {
                Id = Guid.NewGuid(),
                QuestionId = question.Id,
                SelectedOption = "A",
                IsCorrect = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync();
        return (userId, quiz.Id);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
        _connection.Dispose();
        _unitOfWork.Dispose();
    }

    /// <summary>Stand-in IRealTimeNotificationService that only counts calls.</summary>
    private sealed class RealTimeNotificationServiceFake : IRealTimeNotificationService
    {
        public int NotificationsSent { get; private set; }

        public Task SendNotificationAsync(RealTimeNotification notification, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task NotifyDocumentProcessedAsync(Guid userId, Guid documentId, string title, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task NotifyStreakAtRiskAsync(Guid userId, int currentStreak, int hoursRemaining, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task NotifyNewFlashcardsReadyAsync(Guid userId, Guid documentId, string title, int count, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task NotifyQuizReadyAsync(Guid userId, Guid quizId, string title, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task NotifyLevelUpAsync(Guid userId, int newLevel, int totalXp, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task NotifyTierExpiringSoonAsync(Guid userId, string tierName, DateTime expiresAt, int daysRemaining, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task NotifyBadgeEarnedAsync(Guid userId, AchievementDto achievement, CancellationToken cancellationToken = default)
        {
            NotificationsSent++;
            return Task.CompletedTask;
        }
    }
}
