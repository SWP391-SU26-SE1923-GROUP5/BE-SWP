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
            ExhaustiveAdjacentChunkWindow = 2,
            MaxContextChunks = 20
        });
        var pipeline = new RagRetrievalPipeline(
            search.Object, new RagContextExpander(vectors.Object), options);

        var results = await pipeline.RetrieveAsync(
            "BR-01 là gì?", Guid.NewGuid(), null, CancellationToken.None);

        Assert.Equal(10, results.Count);
        search.VerifyAll();
    }
}
