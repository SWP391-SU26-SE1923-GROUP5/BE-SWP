using AIStudyHub.Business.AI.Orchestration;
using AIStudyHub.Business.Interfaces.AI.Orchestration;
using AIStudyHub.Business.Interfaces.AI.Search;
using AIStudyHub.Business.Interfaces.AI.VectorStore;
using Moq;

namespace AIStudyHub.Tests.Services;

public sealed class RagContextExpanderTests
{
    private readonly Mock<IVectorStoreService> _vectors = new();

    [Theory]
    [InlineData("Hãy liệt kê toàn bộ business rules")]
    [InlineData("Cho tôi tất cả các yêu cầu")]
    [InlineData("List all business rules")]
    [InlineData("liệt kê các bussiness rule")]
    [InlineData("liệt kê các business rules của dự án này")]
    public void IsExhaustiveQuery_RecognizesCompleteListIntent(string question)
    {
        Assert.True(RagContextExpander.IsExhaustiveQuery(question));
    }

    [Fact]
    public async Task ExpandAsync_ExhaustiveQuery_AddsAdjacentChunksInDocumentOrder()
    {
        var documentId = Guid.NewGuid();
        var seeds = new[]
        {
            Result(documentId, 1, "Document overview", 1),
            Result(documentId, 4, "BR-04 on page 8", 8)
        };
        _vectors.Setup(x => x.GetPayloadsByDocumentIdAsync(documentId)).ReturnsAsync(
            new[] { Payload(documentId, 1, "Unrelated introduction", 1) }
                .Concat(Enumerable.Range(3, 14)
                .Select(index => Payload(
                    documentId, index, $"BR-{index:D2} rule", index < 8 ? 8 : index < 13 ? 9 : 10)))
                .Append(Payload(documentId, 20, "Unrelated appendix", 12))
                .ToList());
        var expander = new RagContextExpander(_vectors.Object);

        var results = await expander.ExpandAsync(
            "Liệt kê toàn bộ business rules", seeds, adjacentWindow: 1, maxChunks: 20);

        Assert.Equal(Enumerable.Range(3, 14), results.Select(GetChunkIndex));
        Assert.Contains(results, r => GetChunkIndex(r) == 8 && r.Metadata["pageNumber"] == "9");
        Assert.DoesNotContain(results, r => r.Content.Contains("appendix"));
    }

    [Fact]
    public async Task ExpandAsync_NormalQuery_DoesNotLoadOrAddAdjacentChunks()
    {
        var documentId = Guid.NewGuid();
        var seed = Result(documentId, 4, "BR-04", 8);
        var expander = new RagContextExpander(_vectors.Object);

        var results = await expander.ExpandAsync("BR-04 là gì?", [seed], 2, 10);

        Assert.Single(results);
        _vectors.Verify(x => x.GetPayloadsByDocumentIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    private static SearchResult Result(Guid documentId, int chunkIndex, string text, int page) =>
        new(text, 0.9, "srs.pdf", Payload(documentId, chunkIndex, text, page));

    private static Dictionary<string, string> Payload(
        Guid documentId, int chunkIndex, string text, int page) => new()
        {
            ["documentId"] = documentId.ToString(),
            ["chunkIndex"] = chunkIndex.ToString(),
            ["text"] = text,
            ["fileName"] = "srs.pdf",
            ["pageNumber"] = page.ToString(),
            ["contentType"] = "Verbatim",
            ["isHighlightable"] = "True"
        };

    private static int GetChunkIndex(SearchResult result) => int.Parse(result.Metadata["chunkIndex"]);
}
