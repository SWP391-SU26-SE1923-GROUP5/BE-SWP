using AIStudyHub.Business.AI.Orchestration;
using AIStudyHub.Business.Configuration;
using AIStudyHub.Business.Interfaces.AI.Search;
using AIStudyHub.Business.Interfaces.AI.VectorStore;
using Microsoft.Extensions.Options;
using Moq;

namespace AIStudyHub.Tests.Services;

public sealed class RagRetrievalPipelineTests
{
    [Fact]
    public async Task RetrieveAsync_UsesConfiguredCandidateAndContextLimits()
    {
        var search = new Mock<IHybridSearchService>();
        var vectors = new Mock<IVectorStoreService>();
        var candidates = Enumerable.Range(0, 20)
            .Select(index => new SearchResult(
                $"chunk {index}", 1 - index / 100d, "srs.pdf",
                new Dictionary<string, string> { ["chunkIndex"] = index.ToString() }))
            .ToList();
        search.Setup(x => x.SearchAsync(
                "BR-01 là gì?", It.IsAny<Guid>(), null, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidates);
        var options = Options.Create(new RetrievalOptions
        {
            TopK = 50,
            RerankTopK = 10,
            MaxContextChunks = 20
        });
        var pipeline = new RagRetrievalPipeline(
            search.Object, new RagContextExpander(vectors.Object), options);

        var results = await pipeline.RetrieveAsync(
            "BR-01 là gì?", Guid.NewGuid(), null, CancellationToken.None);

        Assert.Equal(10, results.Count);
        search.VerifyAll();
    }

    [Fact]
    public async Task RetrieveAsync_ExhaustiveQuery_DefaultLimitIncludesSixtyThreeChunkDocument()
    {
        var documentId = Guid.NewGuid();
        var search = new Mock<IHybridSearchService>();
        var vectors = new Mock<IVectorStoreService>();
        var seed = new SearchResult("seed", 0.9, "document.pdf", new Dictionary<string, string>
        {
            ["documentId"] = documentId.ToString(),
            ["chunkIndex"] = "0"
        });
        search.Setup(x => x.SearchAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Guid>?>(),
                10, It.IsAny<CancellationToken>()))
            .ReturnsAsync([seed]);
        vectors.Setup(x => x.GetPayloadsByDocumentIdAsync(documentId)).ReturnsAsync(
            Enumerable.Range(0, 63).Select(index => new Dictionary<string, string>
            {
                ["documentId"] = documentId.ToString(),
                ["chunkIndex"] = index.ToString(),
                ["text"] = $"item {index}",
                ["fileName"] = "document.pdf",
                ["contentType"] = "Verbatim"
            }).ToList());
        var pipeline = new RagRetrievalPipeline(
            search.Object,
            new RagContextExpander(vectors.Object),
            Options.Create(new RetrievalOptions()));

        var results = await pipeline.RetrieveAsync(
            "list all items", Guid.NewGuid(), [documentId], CancellationToken.None);

        Assert.Equal(63, results.Count);
    }
}
