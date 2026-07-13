using System;
using System.Linq;
using System.Threading.Tasks;
using AIStudyHub.Business.DTOs.Documents;
using AIStudyHub.Business.Services;
using AIStudyHub.Data;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;
using AIStudyHub.Data.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AIStudyHub.Tests.Services;

public class DocumentServiceTrashBinTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _dbContext;
    private readonly UnitOfWork _unitOfWork;
    private readonly DocumentService _service;

    public DocumentServiceTrashBinTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _connection.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        _dbContext = new ApplicationDbContext(options);
        _dbContext.Database.EnsureCreated();

        _unitOfWork = new UnitOfWork(_dbContext);
        _service = new DocumentService(_unitOfWork, null!);
    }

    private async Task<(Guid UserId, Guid DocId)> SeedDocument(string title = "Test Doc", DocumentLifecycleStatus status = DocumentLifecycleStatus.Active)
    {
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        _dbContext.Users.Add(new User { Id = userId, Email = $"{userId}@test.com", FullName = "User", PasswordHash = "hash" });
        _dbContext.Subjects.Add(new Subject { Id = subjectId, SubjectCode = "T1", SubjectName = "Test" });
        _dbContext.Documents.Add(new Document { Id = docId, UserId = userId, SubjectId = subjectId, Title = title, LifecycleStatus = status });
        await _dbContext.SaveChangesAsync();
        return (userId, docId);
    }

    [Fact]
    public async Task GetAllByUserIdAsync_ExcludesTrashedDocuments()
    {
        var (userId, _) = await SeedDocument("Active Doc", DocumentLifecycleStatus.Active);
        await SeedDocument("Trashed Doc", DocumentLifecycleStatus.Trashed);

        var result = await _service.GetAllByUserIdAsync(userId);

        Assert.Single(result);
        Assert.Equal("Active Doc", result[0].Title);
    }

    [Fact]
    public async Task GetByIdAsync_ExcludesTrashedDocuments()
    {
        var (userId, _) = await SeedDocument("Trashed Doc", DocumentLifecycleStatus.Trashed);

        var result = await _service.GetByIdAsync(userId);

        Assert.Null(result);
    }

    [Fact]
    public async Task TrashAsync_UpdatesLifecycleStatus()
    {
        var (userId, docId) = await SeedDocument();

        await _service.TrashAsync(docId, userId);

        var doc = await _dbContext.Documents.FindAsync(docId);
        Assert.Equal(DocumentLifecycleStatus.Trashed, doc!.LifecycleStatus);
        Assert.NotNull(doc.TrashedAt);
        Assert.Equal(userId, doc.TrashedBy);
    }

    [Fact]
    public async Task TrashAsync_OnlyOwnerCanTrash()
    {
        var (_, docId) = await SeedDocument();
        var otherUserId = Guid.NewGuid();
        _dbContext.Users.Add(new User { Id = otherUserId, Email = "other@test.com", FullName = "Other", PasswordHash = "hash" });
        await _dbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.TrashAsync(docId, otherUserId));
    }

    [Fact]
    public async Task TrashAsync_IsIdempotent()
    {
        var (userId, docId) = await SeedDocument();

        await _service.TrashAsync(docId, userId);
        await _service.TrashAsync(docId, userId); // Should not throw

        var doc = await _dbContext.Documents.FindAsync(docId);
        Assert.Equal(DocumentLifecycleStatus.Trashed, doc!.LifecycleStatus);
    }

    [Fact]
    public async Task RestoreAsync_ResetsLifecycleStatus()
    {
        var (userId, docId) = await SeedDocument(status: DocumentLifecycleStatus.Trashed);

        await _service.RestoreAsync(docId, userId);

        var doc = await _dbContext.Documents.FindAsync(docId);
        Assert.Equal(DocumentLifecycleStatus.Active, doc!.LifecycleStatus);
        Assert.Null(doc.TrashedAt);
        Assert.Null(doc.TrashedBy);
    }

    [Fact]
    public async Task RestoreAsync_CannotRestorePurgedDocument()
    {
        var (userId, docId) = await SeedDocument(status: DocumentLifecycleStatus.Purged);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RestoreAsync(docId, userId));
    }

    [Fact]
    public async Task PurgeAsync_RemovesDocument()
    {
        var (userId, docId) = await SeedDocument(status: DocumentLifecycleStatus.Trashed);

        await _service.PurgeAsync(docId, userId);

        var doc = await _dbContext.Documents.FindAsync(docId);
        Assert.Null(doc);
    }

    [Fact]
    public async Task PurgeAsync_OnlyTrashedCanBePurged()
    {
        var (userId, docId) = await SeedDocument(status: DocumentLifecycleStatus.Active);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.PurgeAsync(docId, userId));
    }

    [Fact]
    public async Task GetTrashAsync_ReturnsOnlyTrashedDocuments()
    {
        var (userId, _) = await SeedDocument("Active Doc", DocumentLifecycleStatus.Active);
        await SeedDocument("Trashed Doc 1", DocumentLifecycleStatus.Trashed);
        await SeedDocument("Trashed Doc 2", DocumentLifecycleStatus.Trashed);

        var result = await _service.GetTrashAsync(userId);

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(DocumentLifecycleStatus.Trashed, r.LifecycleStatus));
    }

    [Fact]
    public async Task ShareAsync_PersistsDocumentShareEntries()
    {
        var (userId, docId) = await SeedDocument();
        var shareeId = Guid.NewGuid();
        _dbContext.Users.Add(new User { Id = shareeId, Email = "sharee@test.com", FullName = "Sharee", PasswordHash = "hash", IsActive = true, Status = "active" });
        await _dbContext.SaveChangesAsync();

        await _service.ShareDocumentAsync(docId, userId,
            new ShareDocumentRequestDto(new() { shareeId }, new() { (int)ShareLevel.Edit }));

        var shares = await _dbContext.DocumentShares.Where(s => s.DocumentId == docId).ToListAsync();
        Assert.Single(shares);
        Assert.Equal(shareeId, shares[0].UserId);
        Assert.Equal(ShareLevel.Edit, shares[0].Level);
    }

    [Fact]
    public async Task GetSharesAsync_OnlyOwnerCanView()
    {
        var (userId, docId) = await SeedDocument();
        var otherId = Guid.NewGuid();
        _dbContext.Users.Add(new User { Id = otherId, Email = "other@test.com", FullName = "Other", PasswordHash = "hash" });
        await _dbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.GetSharesAsync(docId, otherId));
    }

    [Fact]
    public async Task RevokeShareAsync_RemovesShareAndUpdatesStatus()
    {
        var (userId, docId) = await SeedDocument();
        var shareeId = Guid.NewGuid();
        _dbContext.Users.Add(new User { Id = shareeId, Email = "sharee@test.com", FullName = "Sharee", PasswordHash = "hash", IsActive = true, Status = "active" });
        _dbContext.DocumentShares.Add(new DocumentShare { Id = Guid.NewGuid(), DocumentId = docId, UserId = shareeId, SharedBy = userId, SharedAt = DateTime.UtcNow, Level = ShareLevel.Read });
        _dbContext.Documents.Find(docId)!.ShareStatus = "shared";
        await _dbContext.SaveChangesAsync();

        await _service.RevokeShareAsync(docId, shareeId, userId);

        var shares = await _dbContext.DocumentShares.Where(s => s.DocumentId == docId).ToListAsync();
        Assert.Empty(shares);
        var doc = await _dbContext.Documents.FindAsync(docId);
        Assert.Equal("private", doc!.ShareStatus);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
        _connection.Dispose();
        _unitOfWork.Dispose();
    }
}
