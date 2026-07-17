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

        var dto = HybridSearchResultDto.FromSearchResult(result);

        Assert.Equal("Relevant content", dto.Content);
        Assert.Equal(0.91, dto.Score);
        Assert.Equal(documentId, dto.DocumentId);
        Assert.Equal("srs.pdf", dto.FileName);
        Assert.Equal(12, dto.PageNumber);
        Assert.Equal(22, dto.ChunkIndex);
        Assert.Equal("hybrid", dto.MatchType);
        Assert.True(dto.IsHighlightable);
    }
}
