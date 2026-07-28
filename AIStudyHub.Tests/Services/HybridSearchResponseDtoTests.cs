using AIStudyHub.Business.DTOs.Rag;
using AIStudyHub.Business.Interfaces.AI.Search;

namespace AIStudyHub.Tests.Services;

public sealed class HybridSearchResponseDtoTests
{
    [Fact]
    public void FromSearchResult_MapsUiSearchFieldsFromMetadata()
    {
        var documentId = Guid.NewGuid();
        var result = new SearchResult(
            "Relevant content",
            0.91,
            "srs.pdf",
            new Dictionary<string, string>
            {
                ["documentId"] = documentId.ToString(),
                ["fileName"] = "srs.pdf",
                ["pageNumber"] = "12",
                ["chunkIndex"] = "22",
                ["isHighlightable"] = "True",
                ["contentType"] = "Verbatim"
            },
            "hybrid");

        var success = HybridSearchResultDto.TryFromSearchResult(result, out var dto);

        Assert.True(success);
        Assert.NotNull(dto);
        Assert.Equal("Relevant content", dto.Content);
        Assert.Equal(0.91, dto.Score);
        Assert.Equal(documentId, dto.DocumentId);
        Assert.Equal("srs.pdf", dto.FileName);
        Assert.Equal(12, dto.PageNumber);
        Assert.Equal(22, dto.ChunkIndex);
        Assert.Equal("hybrid", dto.MatchType);
        Assert.True(dto.IsHighlightable);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void TryFromSearchResult_InvalidDocumentId_ReturnsFalse(string? rawId)
    {
        var metadata = new Dictionary<string, string>();
        if (rawId is not null)
        {
            metadata["documentId"] = rawId;
        }

        var result = new SearchResult("content", 0.9, "doc.pdf", metadata);

        var success = HybridSearchResultDto.TryFromSearchResult(result, out var dto);

        Assert.False(success);
        Assert.Null(dto);
    }
}
