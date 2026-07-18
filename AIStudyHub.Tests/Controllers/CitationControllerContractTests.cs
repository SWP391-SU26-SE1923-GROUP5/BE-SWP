using System.Security.Claims;
using AIStudyHub.API.Controllers;
using AIStudyHub.Business.Configuration;
using AIStudyHub.Business.DTOs.AIChat;
using AIStudyHub.Business.DTOs.Rag;
using AIStudyHub.Business.Interfaces.AI.Chat;
using AIStudyHub.Business.Interfaces.AI.Generators;
using AIStudyHub.Business.Interfaces.AI.Orchestration;
using AIStudyHub.Business.Interfaces.AI.Search;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Data.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AIStudyHub.Tests.Controllers;

public sealed class CitationControllerContractTests
{
    [Fact]
    public async Task CreateMessage_ForwardsCancellationToken()
    {
        var userId = Guid.NewGuid();
        var request = new CreateChatMessageRequestDto(Guid.NewGuid(), "question");
        using var cts = new CancellationTokenSource();
        var service = new Mock<IAIChatService>();
        service.Setup(item => item.CreateMessageAsync(request, userId, cts.Token))
            .ReturnsAsync(new ChatMessageResponseDto(
                Guid.NewGuid(),
                request.SessionId!.Value,
                "assistant",
                "answer",
                DateTime.UtcNow,
                null,
                true,
                Array.Empty<ChatCitationDto>()));
        var controller = new ChatController(service.Object)
        {
            ControllerContext = CreateControllerContext(userId)
        };

        await controller.CreateMessage(request, cts.Token);

        service.Verify(item => item.CreateMessageAsync(request, userId, cts.Token), Times.Once);
    }

    [Fact]
    public async Task Ask_MalformedDocumentIdentity_OmitsResultAndReportsFilteredCount()
    {
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var search = new Mock<IHybridSearchService>();
        search.Setup(item => item.SearchAsync(
                It.IsAny<string>(),
                userId,
                It.IsAny<IReadOnlyList<Guid>?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Result(documentId.ToString(), "valid.pdf"),
                Result("1", "invalid.pdf")
            });
        var controller = new AIController(
            Mock.Of<ISemanticKernelOrchestrator>(),
            search.Object,
            Options.Create(new RetrievalOptions
            {
                TopK = 10,
                RerankTopK = 5,
                MaxContextChunks = 10
            }),
            Mock.Of<IFlashcardAiService>(),
            Mock.Of<IQuizAiService>(),
            Mock.Of<ILogger<AIController>>(),
            Mock.Of<IRealTimeNotificationService>(),
            Mock.Of<IUnitOfWork>())
        {
            ControllerContext = CreateControllerContext(userId)
        };

        var action = await controller.Ask(
            new HybridSearchRequestDto("question"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var response = Assert.IsType<HybridSearchResponseDto>(ok.Value);
        Assert.Equal(1, response.Count);
        var result = Assert.Single(response.Results);
        Assert.Equal(documentId, result.DocumentId);
    }

    private static ControllerContext CreateControllerContext(Guid userId) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                "test"))
        }
    };

    private static SearchResult Result(string documentId, string source) =>
        new(
            "content",
            0.9,
            source,
            new Dictionary<string, string>
            {
                ["documentId"] = documentId,
                ["fileName"] = source,
                ["chunkIndex"] = "1"
            });
}
