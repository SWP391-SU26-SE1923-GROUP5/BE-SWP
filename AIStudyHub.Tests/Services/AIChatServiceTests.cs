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
using AIStudyHub.Data;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Repositories;
using AutoMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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
    private readonly Mock<IMapper> _mapperMock;
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
        _mapperMock = new Mock<IMapper>();

        // Default: user has quota
        _tokenTrackerMock.Setup(x => x.HasQuotaAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _tokenTrackerMock.Setup(x => x.GetUsageInfoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((0L, 100000L));

        _service = new AIChatService(_unitOfWork, _mapperMock.Object, null!, _orchestratorMock.Object, _tokenTrackerMock.Object);
        _userId = Guid.NewGuid();
    }

    public void Dispose()
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
        await _dbContext.ChatSessions.AddAsync(session);
        await _dbContext.Documents.AddAsync(doc);
        await _dbContext.ChatSessionDocuments.AddAsync(new ChatSessionDocument
        {
            Id = Guid.NewGuid(),
            ChatSessionId = session.Id,
            DocumentId = doc.Id,
            CreatedAt = DateTime.UtcNow
        });
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

        await _dbContext.ChatSessions.AddAsync(session);
        await _dbContext.Documents.AddAsync(doc1);
        await _dbContext.Documents.AddAsync(doc2);
        await _dbContext.ChatSessionDocuments.AddRangeAsync(
            new ChatSessionDocument { Id = Guid.NewGuid(), ChatSessionId = session.Id, DocumentId = doc1.Id, CreatedAt = DateTime.UtcNow },
            new ChatSessionDocument { Id = Guid.NewGuid(), ChatSessionId = session.Id, DocumentId = doc2.Id, CreatedAt = DateTime.UtcNow }
        );
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
    public async Task CreateMessageAsync_QuotaExceeded_ThrowsQuotaExceededException()
    {
        // Arrange
        var session = new ChatSession { Id = Guid.NewGuid(), UserId = _userId, SessionTitle = "Test" };
        await _dbContext.ChatSessions.AddAsync(session);
        await _dbContext.SaveChangesAsync();

        _tokenTrackerMock.Setup(x => x.HasQuotaAsync(_userId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _tokenTrackerMock.Setup(x => x.GetUsageInfoAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((50000L, 50000L));

        var request = new CreateChatMessageRequestDto(session.Id, "Hello");

        // Act & Assert
        await Assert.ThrowsAsync<QuotaExceededException>(() => _service.CreateMessageAsync(request, _userId));
    }
}
