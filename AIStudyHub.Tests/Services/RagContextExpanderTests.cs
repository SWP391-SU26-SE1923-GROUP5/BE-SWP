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
    public void IsExhaustiveQuery_RecognizesCompleteListIntent(string question)
    {
        Assert.True(RagContextExpander.IsExhaustiveQuery(question));
    }

    [Theory]
    [InlineData("liệt kê các bussiness rule")]
    [InlineData("liệt kê các actors trong tài liệu")]
    [InlineData("List API endpoints")]
    [InlineData("Kể ra các rủi ro của dự án")]
    public void IsExhaustiveQuery_UnboundedListIntent_IsGenericAcrossTopics(string question)
    {
        Assert.True(RagContextExpander.IsExhaustiveQuery(question));
    }

    [Theory]
    [InlineData("liệt kê 5 business rules")]
    [InlineData("list top 3 actors")]
    [InlineData("enumerate 10 API endpoints")]
    public void IsExhaustiveQuery_ExplicitlyLimitedList_DoesNotScanCompleteSection(string question)
    {
        Assert.False(RagContextExpander.IsExhaustiveQuery(question));
    }

    [Fact]
    public async Task ExpandAsync_ExhaustiveQuery_LoadsAllSelectedDocumentsInOrder()
    {
        var firstDocumentId = Guid.NewGuid();
        var secondDocumentId = Guid.NewGuid();
        var seeds = new[] { Result(firstDocumentId, 1, "semantic seed", 1) };
        _vectors.Setup(x => x.GetPayloadsByDocumentIdAsync(firstDocumentId)).ReturnsAsync(
        [
            Payload(firstDocumentId, 2, "second chunk", 2),
            Payload(firstDocumentId, 1, "first chunk", 1),
            Payload(firstDocumentId, 3, "generated summary", 2, "Summary")
        ]);
        _vectors.Setup(x => x.GetPayloadsByDocumentIdAsync(secondDocumentId)).ReturnsAsync(
        [
            Payload(secondDocumentId, 1, "actor without a structured identifier", 4),
            Payload(secondDocumentId, 2, "extraction failure", 4, "SystemError")
        ]);
        var expander = new RagContextExpander(_vectors.Object);

        var results = await expander.ExpandAsync(
            "Liệt kê các nội dung trong tài liệu",
            seeds,
            [firstDocumentId, secondDocumentId],
            maxChunks: 20);

        Assert.Equal(
            ["first chunk", "second chunk", "actor without a structured identifier"],
            results.Select(result => result.Content));
        _vectors.Verify(x => x.GetPayloadsByDocumentIdAsync(firstDocumentId), Times.Once);
        _vectors.Verify(x => x.GetPayloadsByDocumentIdAsync(secondDocumentId), Times.Once);
    }

    [Fact]
    public async Task ExpandAsync_NormalQuery_DoesNotLoadOrAddAdjacentChunks()
    {
        var documentId = Guid.NewGuid();
        var seed = Result(documentId, 4, "BR-04", 8);
        var expander = new RagContextExpander(_vectors.Object);

        var results = await expander.ExpandAsync("BR-04 là gì?", [seed], [documentId], 10);

        Assert.Single(results);
        _vectors.Verify(x => x.GetPayloadsByDocumentIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    private static SearchResult Result(Guid documentId, int chunkIndex, string text, int page) =>
        new(text, 0.9, "srs.pdf", Payload(documentId, chunkIndex, text, page));

    private static Dictionary<string, string> Payload(
        Guid documentId, int chunkIndex, string text, int page, string contentType = "Verbatim") => new()
        {
            ["documentId"] = documentId.ToString(),
            ["chunkIndex"] = chunkIndex.ToString(),
            ["text"] = text,
            ["fileName"] = "srs.pdf",
            ["pageNumber"] = page.ToString(),
            ["contentType"] = contentType,
            ["isHighlightable"] = "True"
        };

    private static int GetChunkIndex(SearchResult result) => int.Parse(result.Metadata["chunkIndex"]);
}
