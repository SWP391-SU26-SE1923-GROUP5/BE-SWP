using AIStudyHub.Business.AI.Guardrails;
using AIStudyHub.Business.AI.Orchestration;
using AIStudyHub.Business.Configuration;
using AIStudyHub.Business.DTOs.AI;
using AIStudyHub.Business.Interfaces.AI.Guardrails;
using AIStudyHub.Business.Interfaces.AI.LLM;
using AIStudyHub.Business.Interfaces.AI.Search;
using AIStudyHub.Business.Interfaces.AI.VectorStore;
using AIStudyHub.Data.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AIStudyHub.Tests.Services;

public sealed class SemanticKernelCitationTests
{
    [Fact]
    public async Task AskWithTrackingAsync_AllSourcesHaveInvalidIds_DoesNotCallLlm()
    {
        var search = new Mock<IHybridSearchService>();
        search.Setup(service => service.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<IReadOnlyList<Guid>?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new SearchResult(
                    "content",
                    0.95,
                    "bad.pdf",
                    new Dictionary<string, string>
                    {
                        ["documentId"] = "1",
                        ["chunkIndex"] = "0"
                    })
            });
        var openAi = new Mock<IOpenAIService>();
        openAi.Setup(service => service.SendMessageWithUsageAsync(It.IsAny<string>()))
            .ReturnsAsync(new TokenUsageResult("unexpected answer", 10, 5));
        var orchestrator = CreateOrchestrator(search.Object, openAi.Object);

        var response = await orchestrator.AskWithTrackingAsync(
            Guid.NewGuid(),
            new[] { Guid.NewGuid() },
            "question with enough words",
            Array.Empty<ChatMessage>());

        Assert.False(response.IsRelevant);
        Assert.Empty(response.Citations);
        openAi.Verify(service => service.SendMessageWithUsageAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AskWithTrackingAsync_YesNoShortcut_ReturnsValidatedCitation()
    {
        var documentId = Guid.NewGuid();
        var search = new Mock<IHybridSearchService>();
        search.Setup(service => service.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<IReadOnlyList<Guid>?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new SearchResult(
                    "The backend uses ASP.NET Core and .NET.",
                    0.95,
                    "architecture.pdf",
                    new Dictionary<string, string>
                    {
                        ["documentId"] = documentId.ToString(),
                        ["chunkIndex"] = "0",
                        ["pageNumber"] = "2"
                    })
            });
        var openAi = new Mock<IOpenAIService>();
        var orchestrator = CreateOrchestrator(search.Object, openAi.Object);

        var response = await orchestrator.AskWithTrackingAsync(
            Guid.NewGuid(),
            new[] { documentId },
            "Does it use Java?",
            Array.Empty<ChatMessage>());

        Assert.True(response.IsRelevant);
        var citation = Assert.Single(response.Citations);
        Assert.Equal(1, citation.CitationIndex);
        Assert.Equal(documentId, citation.DocumentId);
        openAi.Verify(service => service.SendMessageWithUsageAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AskWithTrackingAsync_MixedSources_PromptsWithAllowedSourceAndReturnsItsCitation()
    {
        var allowedDocumentId = Guid.NewGuid();
        var outOfSessionDocumentId = Guid.NewGuid();
        var search = new Mock<IHybridSearchService>();
        search.Setup(service => service.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<IReadOnlyList<Guid>?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Result(
                    "The backend architecture uses ASP.NET Core.",
                    0.95,
                    "allowed.pdf",
                    allowedDocumentId.ToString()),
                Result(
                    "INVALID NUMERIC CONTENT MUST NOT REACH PROMPT",
                    0.99,
                    "numeric.pdf",
                    "1"),
                Result(
                    "OUT OF SESSION CONTENT MUST NOT REACH PROMPT",
                    0.99,
                    "other.pdf",
                    outOfSessionDocumentId.ToString())
            });
        string? capturedPrompt = null;
        var openAi = new Mock<IOpenAIService>();
        openAi.Setup(service => service.SendMessageWithUsageAsync(It.IsAny<string>()))
            .Callback<string>(prompt => capturedPrompt = prompt)
            .ReturnsAsync(new TokenUsageResult("grounded answer", 10, 5));
        var orchestrator = CreateOrchestrator(search.Object, openAi.Object);

        var response = await orchestrator.AskWithTrackingAsync(
            Guid.NewGuid(),
            new[] { allowedDocumentId },
            "Explain the backend architecture",
            Array.Empty<ChatMessage>());

        Assert.True(response.IsRelevant);
        Assert.NotNull(capturedPrompt);
        Assert.Contains("The backend architecture uses ASP.NET Core.", capturedPrompt);
        Assert.DoesNotContain("INVALID NUMERIC CONTENT", capturedPrompt);
        Assert.DoesNotContain("OUT OF SESSION CONTENT", capturedPrompt);
        var citation = Assert.Single(response.Citations);
        Assert.Equal(1, citation.CitationIndex);
        Assert.Equal(allowedDocumentId, citation.DocumentId);
    }

    [Fact]
    public async Task AskWithTrackingAsync_LowRelevance_ReturnsNoCitations()
    {
        var documentId = Guid.NewGuid();
        var search = new Mock<IHybridSearchService>();
        search.Setup(service => service.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<IReadOnlyList<Guid>?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Result(
                    "The backend uses ASP.NET Core.",
                    0.1,
                    "architecture.pdf",
                    documentId.ToString())
            });
        var openAi = new Mock<IOpenAIService>();
        openAi.Setup(service => service.SendMessageAsync(It.IsAny<string>()))
            .ReturnsAsync("related topics");
        var orchestrator = CreateOrchestrator(search.Object, openAi.Object);

        var response = await orchestrator.AskWithTrackingAsync(
            Guid.NewGuid(),
            new[] { documentId },
            "astronomy galaxies telescope",
            Array.Empty<ChatMessage>());

        Assert.False(response.IsRelevant);
        Assert.Empty(response.Citations);
        openAi.Verify(service => service.SendMessageWithUsageAsync(It.IsAny<string>()), Times.Never);
    }

    private static SearchResult Result(
        string content,
        double score,
        string source,
        string documentId) =>
        new(
            content,
            score,
            source,
            new Dictionary<string, string>
            {
                ["documentId"] = documentId,
                ["chunkIndex"] = "0"
            });

    private static SemanticKernelOrchestrator CreateOrchestrator(
        IHybridSearchService search,
        IOpenAIService openAi)
    {
        var vectors = new Mock<IVectorStoreService>();
        var pipeline = new RagRetrievalPipeline(
            search,
            new RagContextExpander(vectors.Object),
            Options.Create(new RetrievalOptions
            {
                TopK = 10,
                RerankTopK = 10,
                MaxContextChunks = 10
            }));
        var faithfulness = new Mock<IFaithfulnessFilter>();
        faithfulness.Setup(filter => filter.ValidateAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(true);
        var scorer = new Mock<IConfidenceScorer>();
        scorer.Setup(service => service.Score(
                It.IsAny<string>(),
                It.IsAny<GroundingResult>(),
                It.IsAny<bool>()))
            .Returns(1);

        return new SemanticKernelOrchestrator(
            pipeline,
            vectors.Object,
            faithfulness.Object,
            Mock.Of<IGroundingVerifier>(),
            scorer.Object,
            Options.Create(new SemanticKernelOptions()),
            openAi,
            new RagCitationFactory(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<RagCitationFactory>.Instance),
            Mock.Of<ILogger<SemanticKernelOrchestrator>>());
    }
}
