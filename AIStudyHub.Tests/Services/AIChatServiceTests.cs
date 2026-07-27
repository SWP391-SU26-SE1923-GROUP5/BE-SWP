using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AIStudyHub.Business.AI.Chat;
using AIStudyHub.Business.DTOs.AIChat;
using AIStudyHub.Business.Exceptions;
using AIStudyHub.Business.Interfaces.AI.Chat;
using AIStudyHub.Business.Interfaces.AI.LLM;
using AIStudyHub.Business.Interfaces.AI.Orchestration;
using AIStudyHub.Business.Interfaces.AI.Tracking;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Mappings;
using AIStudyHub.Data;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Repositories;
using AutoMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AIStudyHub.Tests.Services;

public class AIChatServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _dbContext;
    private readonly UnitOfWork _unitOfWork;
    private readonly Mock<ITokenTrackerService> _tokenTrackerMock;
    private readonly Mock<ISemanticKernelOrchestrator> _orchestratorMock;
    private readonly IMapper _mapper;
    private readonly AIChatService _service;
    private readonly Guid _userId;

    public AIChatServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        _dbContext = new ApplicationDbContext(options);
        _dbContext.Database.EnsureCreated();

        _unitOfWork = new UnitOfWork(_dbContext);
        _tokenTrackerMock = new Mock<ITokenTrackerService>();
        _orchestratorMock = new Mock<ISemanticKernelOrchestrator>();
        var mapperConfig = new MapperConfigurationExpression();
        mapperConfig.AddProfile<ApplicationMappingProfile>();
        _mapper = new MapperConfiguration(
            mapperConfig,
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance).CreateMapper();

        // Default: user has quota
        _tokenTrackerMock.Setup(x => x.HasQuotaAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _tokenTrackerMock.Setup(x => x.GetUsageInfoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((0, 100000));

        _service = new AIChatService(
            _unitOfWork,
            _mapper,
            null!,
            _orchestratorMock.Object,
            _tokenTrackerMock.Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AIChatService>.Instance);
        _userId = Guid.NewGuid();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task CreateMessageAsync_SessionWithNoDocuments_ReturnsWarningMessage()
    {
        // Arrange
        var session = new ChatSession { Id = Guid.NewGuid(), UserId = _userId, SessionTitle = "Test" };
        await _dbContext.ChatSessions.AddAsync(session);
        await _dbContext.SaveChangesAsync();

        _orchestratorMock.Setup(x => x.AskWithTrackingAsync(
            _userId, null, It.IsAny<string>(), It.IsAny<IReadOnlyList<ChatMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RagResponseWithUsage("no doc", new(), 0, 0, 0, false));

        var request = new CreateChatMessageRequestDto(session.Id, "What is AI?");

        // Act
        var result = await _service.CreateMessageAsync(request, _userId);

        // Assert
        Assert.Equal("Vui lòng đính kèm một tài liệu để tôi có thể trả lời câu hỏi của bạn dựa trên nội dung tài liệu.", result.Content);
    }

    [Fact]
    public async Task CreateMessageAsync_SessionWithOneDocument_PassesSingleDocIdToOrchestrator()
    {
        // Arrange
        var session = new ChatSession { Id = Guid.NewGuid(), UserId = _userId, SessionTitle = "Test" };
        var doc = new Document { Id = Guid.NewGuid(), UserId = _userId, Title = "Doc 1" };
        session.ChatSessionDocuments.Add(new ChatSessionDocument
        {
            Id = Guid.NewGuid(),
            ChatSessionId = session.Id,
            DocumentId = doc.Id,
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.ChatSessions.AddAsync(session);
        await _dbContext.Documents.AddAsync(doc);
        await _dbContext.SaveChangesAsync();

        _orchestratorMock.Setup(x => x.AskWithTrackingAsync(
            _userId, It.Is<IReadOnlyList<Guid>>(ids => ids != null && ids.Count == 1 && ids[0] == doc.Id),
            It.IsAny<string>(), It.IsAny<IReadOnlyList<ChatMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RagResponseWithUsage("answer", new(), 0.9, 100, 50, true));

        var request = new CreateChatMessageRequestDto(session.Id, "Summarize");

        // Act
        var result = await _service.CreateMessageAsync(request, _userId);

        // Assert
        Assert.Equal("answer", result.Content);
        _orchestratorMock.Verify(x => x.AskWithTrackingAsync(
            _userId, It.Is<IReadOnlyList<Guid>>(ids => ids != null && ids.Count == 1),
            "Summarize", It.IsAny<IReadOnlyList<ChatMessage>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateMessageAsync_SessionWithTwoDocuments_PassesBothDocIdsToOrchestrator()
    {
        // Arrange
        var session = new ChatSession { Id = Guid.NewGuid(), UserId = _userId, SessionTitle = "Multi-doc Test" };
        var doc1 = new Document { Id = Guid.NewGuid(), UserId = _userId, Title = "Doc 1" };
        var doc2 = new Document { Id = Guid.NewGuid(), UserId = _userId, Title = "Doc 2" };
        session.ChatSessionDocuments.Add(new ChatSessionDocument { Id = Guid.NewGuid(), ChatSessionId = session.Id, DocumentId = doc1.Id, CreatedAt = DateTime.UtcNow });
        session.ChatSessionDocuments.Add(new ChatSessionDocument { Id = Guid.NewGuid(), ChatSessionId = session.Id, DocumentId = doc2.Id, CreatedAt = DateTime.UtcNow });

        await _dbContext.ChatSessions.AddAsync(session);
        await _dbContext.Documents.AddAsync(doc1);
        await _dbContext.Documents.AddAsync(doc2);
        await _dbContext.SaveChangesAsync();

        _orchestratorMock.Setup(x => x.AskWithTrackingAsync(
            _userId,
            It.Is<IReadOnlyList<Guid>>(ids => ids != null && ids.Count == 2 && ids.Contains(doc1.Id) && ids.Contains(doc2.Id)),
            It.IsAny<string>(), It.IsAny<IReadOnlyList<ChatMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RagResponseWithUsage("cross-doc answer", new(), 0.9, 100, 50, true));

        var request = new CreateChatMessageRequestDto(session.Id, "Compare both documents");

        // Act
        var result = await _service.CreateMessageAsync(request, _userId);

        // Assert
        Assert.Equal("cross-doc answer", result.Content);
        _orchestratorMock.Verify(x => x.AskWithTrackingAsync(
            _userId,
            It.Is<IReadOnlyList<Guid>>(ids => ids != null && ids.Count == 2),
            "Compare both documents",
            It.IsAny<IReadOnlyList<ChatMessage>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateMessageAsync_RagCitations_PreserveDocumentIdentityAndDisplayMetadata()
    {
        var session = new ChatSession { Id = Guid.NewGuid(), UserId = _userId, SessionTitle = "Citation Test" };
        var firstDocumentId = Guid.NewGuid();
        var secondDocumentId = Guid.NewGuid();
        var firstDocument = new Document { Id = firstDocumentId, UserId = _userId, Title = "First" };
        var secondDocument = new Document { Id = secondDocumentId, UserId = _userId, Title = "Second" };
        session.ChatSessionDocuments.Add(new ChatSessionDocument
        {
            Id = Guid.NewGuid(), ChatSessionId = session.Id, DocumentId = firstDocumentId, CreatedAt = DateTime.UtcNow
        });
        session.ChatSessionDocuments.Add(new ChatSessionDocument
        {
            Id = Guid.NewGuid(), ChatSessionId = session.Id, DocumentId = secondDocumentId, CreatedAt = DateTime.UtcNow
        });
        await _dbContext.AddRangeAsync(session, firstDocument, secondDocument);
        await _dbContext.SaveChangesAsync();

        var longContent = new string('x', 301);
        _orchestratorMock.Setup(x => x.AskWithTrackingAsync(
                _userId,
                It.IsAny<IReadOnlyList<Guid>>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RagResponseWithUsage(
                "answer",
                new List<CitationInfo>
                {
                    new(firstDocumentId, "abc.pdf", longContent, 0.91, 3, "hybrid", true, null, 1),
                    new(secondDocumentId, "abc (1).pdf", "short", 0.82, null, "semantic", false, "legacy_unclassified", 2)
                },
                0.9,
                10,
                5,
                true));

        var result = await _service.CreateMessageAsync(
            new CreateChatMessageRequestDto(session.Id, "Compare sources"), _userId);

        Assert.NotNull(result.Citations);
        Assert.Collection(result.Citations,
            first =>
            {
                Assert.Equal(1, first.CitationIndex);
                Assert.Equal(firstDocumentId, first.DocumentId);
                Assert.Equal("abc.pdf", first.Source);
                Assert.Equal(new string('x', 300), first.Snippet);
                Assert.Equal(3, first.PageNumber);
                Assert.Equal(0.91, first.Relevance);
                Assert.Equal("hybrid", first.MatchType);
                Assert.True(first.IsHighlightable);
                Assert.Null(first.Reason);
            },
            second =>
            {
                Assert.Equal(2, second.CitationIndex);
                Assert.Equal(secondDocumentId, second.DocumentId);
                Assert.Equal("abc (1).pdf", second.Source);
                Assert.Equal("short", second.Snippet);
                Assert.Null(second.PageNumber);
                Assert.Equal(0.82, second.Relevance);
                Assert.Equal("semantic", second.MatchType);
                Assert.False(second.IsHighlightable);
                Assert.Equal("legacy_unclassified", second.Reason);
            });

        _dbContext.ChangeTracker.Clear();
        var persisted = await _dbContext.ChatMessageCitations
            .OrderBy(citation => citation.CitationIndex)
            .ToListAsync();

        Assert.Collection(persisted,
            first =>
            {
                Assert.Equal(1, first.CitationIndex);
                Assert.Equal(firstDocumentId, first.DocumentId);
                Assert.Equal("abc.pdf", first.Source);
                Assert.Equal(result.Citations[0].Snippet, first.Snippet);
                Assert.Equal(3, first.PageNumber);
                Assert.True(first.IsHighlightable);
            },
            second =>
            {
                Assert.Equal(2, second.CitationIndex);
                Assert.Equal(secondDocumentId, second.DocumentId);
                Assert.Equal("legacy_unclassified", second.Reason);
            });

        var history = await _service.GetMessagesAsync(session.Id, _userId);
        var reloadedAssistant = Assert.Single(history, message => message.Sender == "assistant");
        Assert.True(result.IsRelevant);
        Assert.True(reloadedAssistant.IsRelevant);
        Assert.Collection(reloadedAssistant.Citations,
            first =>
            {
                Assert.Equal(1, first.CitationIndex);
                Assert.Equal(firstDocumentId, first.DocumentId);
            },
            second =>
            {
                Assert.Equal(2, second.CitationIndex);
                Assert.Equal(secondDocumentId, second.DocumentId);
            });
    }

    [Fact]
    public async Task GetMessagesAsync_SessionOwnedByAnotherUser_ThrowsNotFound()
    {
        var session = new ChatSession
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            SessionTitle = "Private"
        };
        await _dbContext.ChatSessions.AddAsync(session);
        await _dbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.GetMessagesAsync(session.Id, _userId));
    }

    [Fact]
    public async Task CreateMessageAsync_CitationSaveFails_DoesNotPersistPartialAssistantMessage()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new FailCitationSaveInterceptor())
            .Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var unitOfWork = new UnitOfWork(db);
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var session = new ChatSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SessionTitle = "Atomic citation"
        };
        var document = new Document
        {
            Id = documentId,
            UserId = userId,
            Title = "Source"
        };
        session.ChatSessionDocuments.Add(new ChatSessionDocument
        {
            Id = Guid.NewGuid(),
            ChatSessionId = session.Id,
            DocumentId = documentId,
            CreatedAt = DateTime.UtcNow
        });
        await db.AddRangeAsync(session, document);
        await db.SaveChangesAsync();

        var orchestrator = new Mock<ISemanticKernelOrchestrator>();
        orchestrator.Setup(service => service.AskWithTrackingAsync(
                userId,
                It.IsAny<IReadOnlyList<Guid>>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RagResponseWithUsage(
                "answer",
                new List<CitationInfo>
                {
                    new(documentId, "source.pdf", "exact", 0.9, 1, "hybrid", true, null, 1)
                },
                0.9,
                10,
                5,
                true));
        var tokenTracker = new Mock<ITokenTrackerService>();
        tokenTracker.Setup(service => service.HasQuotaAsync(
                userId,
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        tokenTracker.Setup(service => service.RecordUsageAsync(
                userId,
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = new AIChatService(
            unitOfWork,
            _mapper,
            null!,
            orchestrator.Object,
            tokenTracker.Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AIChatService>.Instance);

        await Assert.ThrowsAsync<DbUpdateException>(() => service.CreateMessageAsync(
            new CreateChatMessageRequestDto(session.Id, "question"),
            userId));

        Assert.Single(await db.ChatMessages
            .Where(message => message.Sender == "user")
            .ToListAsync());
        Assert.Empty(await db.ChatMessages
            .Where(message => message.Sender == "assistant")
            .ToListAsync());
        Assert.Empty(await db.ChatMessageCitations.ToListAsync());
    }

    [Fact]
    public async Task UpdateSessionAsync_OwnerRenamesSessionAndTrimsTitle()
    {
        var session = new ChatSession
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            SessionTitle = "Old title"
        };
        await _dbContext.ChatSessions.AddAsync(session);
        await _dbContext.SaveChangesAsync();

        var result = await _service.UpdateSessionAsync(
            session.Id,
            new UpdateChatSessionRequestDto("  New title  "),
            _userId);

        Assert.Equal("New title", result.SessionTitle);
        Assert.Equal("New title", (await _dbContext.ChatSessions.FindAsync(session.Id))!.SessionTitle);
    }

    [Fact]
    public async Task UpdateSessionAsync_OtherUserCannotRenameSession()
    {
        var session = new ChatSession
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            SessionTitle = "Private"
        };
        await _dbContext.ChatSessions.AddAsync(session);
        await _dbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.UpdateSessionAsync(
            session.Id,
            new UpdateChatSessionRequestDto("Changed"),
            _userId));
    }

    [Fact]
    public async Task DeleteSessionAsync_OwnerDeletesSession()
    {
        var session = new ChatSession
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            SessionTitle = "Delete me"
        };
        await _dbContext.ChatSessions.AddAsync(session);
        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ChatSessionId = session.Id,
            Sender = "assistant",
            Content = "Answer"
        };
        message.Citations.Add(new ChatMessageCitation
        {
            Id = Guid.NewGuid(),
            CitationIndex = 1,
            DocumentId = Guid.NewGuid(),
            Source = "source.pdf",
            Snippet = "Quoted text",
            MatchType = "hybrid"
        });
        await _dbContext.ChatMessages.AddAsync(message);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
        await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");

        await _service.DeleteSessionAsync(session.Id, _userId);

        Assert.Null(await _dbContext.ChatSessions.FindAsync(session.Id));
        Assert.Empty(await _dbContext.ChatMessages.ToListAsync());
        Assert.Empty(await _dbContext.ChatMessageCitations.ToListAsync());
    }

    [Fact]
    public async Task CreateMessageAsync_QuotaExceeded_ThrowsQuotaExceededException()
    {
        // Arrange
        var session = new ChatSession { Id = Guid.NewGuid(), UserId = _userId, SessionTitle = "Test" };
        await _dbContext.ChatSessions.AddAsync(session);
        await _dbContext.SaveChangesAsync();

        _tokenTrackerMock.Setup(x => x.HasQuotaAsync(_userId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _tokenTrackerMock.Setup(x => x.GetUsageInfoAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((50000, 50000));

        var request = new CreateChatMessageRequestDto(session.Id, "Hello");

        // Act & Assert
        await Assert.ThrowsAsync<QuotaExceededException>(() => _service.CreateMessageAsync(request, _userId));
    }

    [Fact]
    public async Task CreateMessageAsync_CanceledBeforeSessionLookup_DoesNotPersistMessage()
    {
        var session = new ChatSession
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            SessionTitle = "Canceled request"
        };
        await _dbContext.ChatSessions.AddAsync(session);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _service.CreateMessageAsync(
                new CreateChatMessageRequestDto(session.Id, "question"),
                _userId,
                cts.Token));

        _dbContext.ChangeTracker.Clear();
        Assert.Empty(await _dbContext.ChatMessages.ToListAsync());
    }

    private sealed class FailCitationSaveInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<ChatMessageCitation>()
                .Any(entry => entry.State == EntityState.Added))
            {
                throw new DbUpdateException("Injected citation save failure");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
