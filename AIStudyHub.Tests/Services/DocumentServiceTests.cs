using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AIStudyHub.Business.DTOs.Documents;
using AIStudyHub.Business.Services;
using AIStudyHub.Data;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;
using AIStudyHub.Data.Repositories;
using AutoMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AIStudyHub.Tests.Services;

public class DocumentServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _dbContext;
    private readonly UnitOfWork _unitOfWork;
    private readonly Mock<IMapper> _mapperMock;
    private readonly DocumentService _service;

    public DocumentServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        _dbContext = new ApplicationDbContext(options);
        _dbContext.Database.EnsureCreated();

        _unitOfWork = new UnitOfWork(_dbContext);
        _mapperMock = new Mock<IMapper>();

        _service = new DocumentService(_unitOfWork, _mapperMock.Object);
    }

    [Fact]
    public async Task GetAllByUserIdAsync_ReturnsUserDocumentsAndPublic()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        _dbContext.Users.Add(new User { Id = userId, Email = "test1@test.com", FullName = "Test User 1", PasswordHash = "hash" });
        _dbContext.Users.Add(new User { Id = otherUserId, Email = "test2@test.com", FullName = "Test User 2", PasswordHash = "hash" });
        _dbContext.Subjects.Add(new Subject { Id = subjectId, SubjectCode = "S1", SubjectName = "Subject 1" });
        
        _dbContext.Documents.Add(new Document { Id = Guid.NewGuid(), UserId = userId, SubjectId = subjectId, Title = "My Doc", ShareStatus = "private" });
        _dbContext.Documents.Add(new Document { Id = Guid.NewGuid(), UserId = otherUserId, SubjectId = subjectId, Title = "Public Doc", ShareStatus = "public" });
        _dbContext.Documents.Add(new Document { Id = Guid.NewGuid(), UserId = otherUserId, SubjectId = subjectId, Title = "Other Private Doc", ShareStatus = "private" });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetAllByUserIdAsync(userId);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, d => d.Title == "My Doc");
        Assert.Contains(result, d => d.Title == "Public Doc");
        Assert.DoesNotContain(result, d => d.Title == "Other Private Doc");
    }

    [Fact]
    public async Task GetByIdAsync_DocumentExists_ReturnsDocument()
    {
        // Arrange
        var docId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        _dbContext.Users.Add(new User { Id = userId, Email = "test3@test.com", FullName = "Test User 3", PasswordHash = "hash" });
        _dbContext.Subjects.Add(new Subject { Id = subjectId, SubjectCode = "S2", SubjectName = "Subject 2" });
        
        _dbContext.Documents.Add(new Document { Id = docId, UserId = userId, SubjectId = subjectId, Title = "Test Doc" });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetByIdAsync(docId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Doc", result.Title);
    }

    [Fact]
    public async Task DeleteAsync_ExistingDocument_RemovesDocument()
    {
        // Arrange
        var docId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        _dbContext.Users.Add(new User { Id = userId, Email = "test4@test.com", FullName = "Test User 4", PasswordHash = "hash" });
        _dbContext.Subjects.Add(new Subject { Id = subjectId, SubjectCode = "S3", SubjectName = "Subject 3" });

        _dbContext.Documents.Add(new Document { Id = docId, UserId = userId, SubjectId = subjectId, Title = "Test Doc" });
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.DeleteAsync(docId);
        
        // Assert
        var docInDb = await _dbContext.Documents.FindAsync(docId);
        Assert.Null(docInDb);
    }

    [Fact]
    public async Task GetAvailableFileNameAsync_DuplicateActiveNames_ReturnsSmallestAvailableSuffixCaseInsensitively()
    {
        var userId = Guid.NewGuid();
        await SeedDocumentsAsync(userId,
            ("abc.pdf", DocumentLifecycleStatus.Active),
            ("ABC (1).PDF", DocumentLifecycleStatus.Active));

        var result = await _service.GetAvailableFileNameAsync(userId, "abc.pdf");

        Assert.Equal("abc (2).pdf", result);
    }

    [Fact]
    public async Task GetAvailableFileNameAsync_TrashedAndOtherUserNames_DoNotReserveName()
    {
        var userId = Guid.NewGuid();
        await SeedDocumentsAsync(userId, ("abc.pdf", DocumentLifecycleStatus.Trashed));
        await SeedDocumentsAsync(Guid.NewGuid(), ("abc.pdf", DocumentLifecycleStatus.Active));

        var result = await _service.GetAvailableFileNameAsync(userId, "abc.pdf");

        Assert.Equal("abc.pdf", result);
    }

    [Theory]
    [InlineData("archive.tar.gz", "archive.tar (1).gz")]
    [InlineData("README", "README (1)")]
    [InlineData("abc (1).pdf", "abc (1) (1).pdf")]
    public async Task GetAvailableFileNameAsync_PreservesLiteralStemAndExtension(string requested, string expected)
    {
        var userId = Guid.NewGuid();
        await SeedDocumentsAsync(userId, (requested, DocumentLifecycleStatus.Active));

        var result = await _service.GetAvailableFileNameAsync(userId, requested);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task GetAvailableFileNameAsync_ExcludedDocument_DoesNotReserveItsOwnName()
    {
        var userId = Guid.NewGuid();
        var documentId = await SeedDocumentsAsync(userId, ("abc.pdf", DocumentLifecycleStatus.Trashed));

        var result = await _service.GetAvailableFileNameAsync(userId, "abc.pdf", documentId);

        Assert.Equal("abc.pdf", result);
    }

    [Fact]
    public async Task GetAvailableFileNameAsync_PathAndLongStem_ReturnsSafeNameWithinDatabaseLimit()
    {
        var userId = Guid.NewGuid();
        var longName = new string('a', 251) + ".pdf";
        await SeedDocumentsAsync(userId, (longName, DocumentLifecycleStatus.Active));

        var result = await _service.GetAvailableFileNameAsync(userId, $"folder/{longName}");

        Assert.Equal(255, result.Length);
        Assert.EndsWith(" (1).pdf", result);
        Assert.DoesNotContain("folder", result);
    }

    [Fact]
    public void DocumentModel_ActiveFilenameIndex_IsOwnerScopedFilteredAndUnique()
    {
        var documentType = _dbContext.Model.FindEntityType(typeof(Document));
        var index = documentType!.GetIndexes().SingleOrDefault(candidate =>
            candidate.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(Document.UserId), nameof(Document.FileName) }));

        Assert.NotNull(index);
        Assert.True(index.IsUnique);
        Assert.Equal("[LifecycleStatus] = 0 AND [file_name] IS NOT NULL", index.GetFilter());
    }

    private async Task<Guid> SeedDocumentsAsync(
        Guid userId,
        params (string FileName, DocumentLifecycleStatus LifecycleStatus)[] documents)
    {
        if (!await _dbContext.Users.AnyAsync(x => x.Id == userId))
        {
            _dbContext.Users.Add(new User
            {
                Id = userId,
                Email = $"{userId}@test.com",
                FullName = "Filename Test User",
                PasswordHash = "hash"
            });
        }

        var subjectId = Guid.NewGuid();
        _dbContext.Subjects.Add(new Subject
        {
            Id = subjectId,
            SubjectCode = $"S_{subjectId:N}"[..20],
            SubjectName = "Filename Test Subject"
        });

        var firstId = Guid.Empty;
        foreach (var (fileName, lifecycleStatus) in documents)
        {
            var document = new Document
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SubjectId = subjectId,
                Title = fileName,
                FileName = fileName,
                LifecycleStatus = lifecycleStatus
            };
            firstId = firstId == Guid.Empty ? document.Id : firstId;
            _dbContext.Documents.Add(document);
        }

        await _dbContext.SaveChangesAsync();
        return firstId;
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
        _connection.Dispose();
        _unitOfWork.Dispose();
    }
}
