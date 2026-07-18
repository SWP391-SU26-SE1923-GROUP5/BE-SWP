using AIStudyHub.Business.AI.Orchestration;
using AIStudyHub.Business.Configuration;
using AIStudyHub.Business.DTOs.AI;
using AIStudyHub.Business.Interfaces.AI.Guardrails;
using AIStudyHub.Business.Interfaces.AI.LLM;
using AIStudyHub.Business.Interfaces.AI.Orchestration;
using AIStudyHub.Business.Interfaces.AI.Search;
using AIStudyHub.Business.Interfaces.AI.VectorStore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AIStudyHub.Tests.Services;

public sealed class SemanticKernelStructuredExhaustiveTests
{
    [Fact]
    public async Task AskWithTrackingAsync_ExhaustiveAnswer_UsesAllChunksWithoutStructuredIdShortcut()
    {
        var documentId = Guid.NewGuid();
        var search = new Mock<IHybridSearchService>();
        var vectors = new Mock<IVectorStoreService>();
        var openAi = new Mock<IOpenAIService>();
        var seed = Result(documentId, 15, "3. Business Rules BR-01 First rule.", 8);
        search.Setup(x => x.SearchAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Guid>?>(),
                50, It.IsAny<CancellationToken>()))
            .ReturnsAsync([seed]);
        vectors.Setup(x => x.GetPayloadsByDocumentIdAsync(documentId)).ReturnsAsync(
        [
            seed.Metadata,
            Result(documentId, 16, "An actor without a structured identifier.", 9).Metadata
        ]);
        openAi.Setup(x => x.SendMessageWithUsageAsync(It.IsAny<string>()))
            .ReturnsAsync(new TokenUsageResult("Complete answer", 120, 20));
        var retrievalOptions = Options.Create(new RetrievalOptions
        {
            TopK = 50,
            RerankTopK = 10,
            MaxContextChunks = 50
        });
        var pipeline = new RagRetrievalPipeline(
            search.Object, new RagContextExpander(vectors.Object), retrievalOptions);
        var faithfulness = new Mock<IFaithfulnessFilter>();
        faithfulness.Setup(x => x.ValidateAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(true);
        var scorer = new Mock<IConfidenceScorer>();
        scorer.Setup(x => x.Score(It.IsAny<string>(), It.IsAny<AIStudyHub.Business.AI.Guardrails.GroundingResult>(), true))
            .Returns(1);
        var orchestrator = new SemanticKernelOrchestrator(
            pipeline,
            vectors.Object,
            faithfulness.Object,
            Mock.Of<IGroundingVerifier>(),
            scorer.Object,
            Options.Create(new SemanticKernelOptions()),
            openAi.Object,
            new RagCitationFactory(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<RagCitationFactory>.Instance),
            Mock.Of<ILogger<SemanticKernelOrchestrator>>());

        var response = await orchestrator.AskWithTrackingAsync(
            Guid.NewGuid(), [documentId], "liệt kê toàn bộ business rules", []);

        Assert.Equal("Complete answer", response.Answer);
        Assert.Equal(120, response.InputTokens);
        Assert.Equal(20, response.OutputTokens);
        openAi.Verify(x => x.SendMessageWithUsageAsync(
            It.Is<string>(prompt =>
                prompt.Contains("BR-01 First rule.")
                && prompt.Contains("An actor without a structured identifier.")
                && prompt.Contains("Inspect every provided source chunk")
                && prompt.Contains("Do not append follow-up offers"))), Times.Once);
    }

    private static SearchResult Result(Guid documentId, int chunkIndex, string text, int page) =>
        new(text, 0.9, "srs.pdf", new Dictionary<string, string>
        {
            ["documentId"] = documentId.ToString(),
            ["chunkIndex"] = chunkIndex.ToString(),
            ["text"] = text,
            ["fileName"] = "srs.pdf",
            ["pageNumber"] = page.ToString(),
            ["contentType"] = "Verbatim",
            ["isHighlightable"] = "True"
        });
}
