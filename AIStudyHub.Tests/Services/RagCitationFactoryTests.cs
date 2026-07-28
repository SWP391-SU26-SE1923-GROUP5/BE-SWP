using AIStudyHub.Business.AI.Orchestration;
using AIStudyHub.Business.Interfaces.AI.Search;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudyHub.Tests.Services;

public sealed class RagCitationFactoryTests
{
    private readonly RagCitationFactory _factory = new(
        NullLogger<RagCitationFactory>.Instance);

    [Fact]
    public void Create_ValidResults_AssignsIndicesAndPreservesDocumentIds()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var results = new[]
        {
            Result(firstId.ToString(), "a.pdf", "first", "2", "7"),
            Result(secondId.ToString(), "b.pdf", "second", "4", "9")
        };

        var sources = _factory.Create(results, new[] { firstId, secondId });

        Assert.Collection(sources,
            first =>
            {
                Assert.Equal(1, first.Citation.CitationIndex);
                Assert.Equal(firstId, first.Citation.DocumentId);
                Assert.Equal(2, first.Citation.PageNumber);
            },
            second =>
            {
                Assert.Equal(2, second.Citation.CitationIndex);
                Assert.Equal(secondId, second.Citation.DocumentId);
            });
    }

    [Fact]
    public void Create_InvalidEmptyOrUnauthorizedIds_RejectsResults()
    {
        var allowedId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var results = new[]
        {
            Result("1", "numeric.pdf", "numeric", "1", "1"),
            Result(Guid.Empty.ToString(), "empty.pdf", "empty", "1", "2"),
            Result(otherId.ToString(), "other.pdf", "other", "1", "3"),
            Result(allowedId.ToString(), "allowed.pdf", "allowed", "1", "4")
        };

        var source = Assert.Single(_factory.Create(results, new[] { allowedId }));

        Assert.Equal(allowedId, source.Citation.DocumentId);
        Assert.Equal(1, source.Citation.CitationIndex);
    }

    [Fact]
    public void Create_DuplicateChunk_KeepsFirstOccurrenceAndContiguousIndices()
    {
        var documentId = Guid.NewGuid();
        var results = new[]
        {
            Result(documentId.ToString(), "doc.pdf", "first", "3", "8"),
            Result(documentId.ToString(), "doc.pdf", "duplicate", "3", "8"),
            Result(documentId.ToString(), "doc.pdf", "next", "3", "9")
        };

        var sources = _factory.Create(results, new[] { documentId });

        Assert.Equal(new[] { "first", "next" }, sources.Select(source => source.Result.Content));
        Assert.Equal(new[] { 1, 2 }, sources.Select(source => source.Citation.CitationIndex));
    }

    [Fact]
    public void Create_DuplicateWithoutChunkIndex_UsesDocumentPageAndContent()
    {
        var documentId = Guid.NewGuid();
        var metadata = new Dictionary<string, string>
        {
            ["documentId"] = documentId.ToString(),
            ["pageNumber"] = "3"
        };
        var results = new[]
        {
            new SearchResult("same", 0.9, "doc.pdf", new Dictionary<string, string>(metadata)),
            new SearchResult("same", 0.8, "doc.pdf", new Dictionary<string, string>(metadata)),
            new SearchResult("different", 0.7, "doc.pdf", new Dictionary<string, string>(metadata))
        };

        var sources = _factory.Create(results, new[] { documentId });

        Assert.Equal(new[] { "same", "different" }, sources.Select(source => source.Result.Content));
        Assert.Equal(new[] { 1, 2 }, sources.Select(source => source.Citation.CitationIndex));
    }

    [Theory]
    [InlineData("", "content")]
    [InlineData("doc.pdf", "")]
    [InlineData(" ", "content")]
    [InlineData("doc.pdf", " ")]
    public void Create_BlankSourceOrContent_RejectsResult(string source, string content)
    {
        var documentId = Guid.NewGuid();
        var result = Result(documentId.ToString(), source, content, "1", "1");

        var sources = _factory.Create(new[] { result }, new[] { documentId });

        Assert.Empty(sources);
    }

    private static SearchResult Result(
        string documentId,
        string source,
        string content,
        string pageNumber,
        string chunkIndex) =>
        new(content, 0.9, source, new Dictionary<string, string>
        {
            ["documentId"] = documentId,
            ["pageNumber"] = pageNumber,
            ["chunkIndex"] = chunkIndex,
            ["contentType"] = "Verbatim",
            ["isHighlightable"] = "True"
        }, "hybrid");
}
