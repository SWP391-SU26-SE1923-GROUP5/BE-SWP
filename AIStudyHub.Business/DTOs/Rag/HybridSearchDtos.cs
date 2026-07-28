using AIStudyHub.Business.AI.Orchestration;
using AIStudyHub.Business.Interfaces.AI.Search;

namespace AIStudyHub.Business.DTOs.Rag;

public sealed record HybridSearchRequestDto(
    string Question,
    IReadOnlyList<Guid>? DocumentIds = null,
    int? TopK = null);

public sealed record HybridSearchResponseDto(
    string Query,
    int Count,
    IReadOnlyList<HybridSearchResultDto> Results);

public sealed record HybridSearchResultDto(
    string Content,
    double Score,
    Guid DocumentId,
    string FileName,
    int? PageNumber,
    int? ChunkIndex,
    string MatchType,
    bool IsHighlightable)
{
    public static HybridSearchResultDto FromSearchResult(SearchResult result)
    {
        var highlight = CitationHighlightability.FromMetadata(result.Metadata);
        return new HybridSearchResultDto(
            result.Content,
            result.Score,
            ParseGuid(result.Metadata.GetValueOrDefault("documentId")),
            result.Metadata.GetValueOrDefault("fileName", result.Source),
            ParseInt(result.Metadata.GetValueOrDefault("pageNumber")),
            ParseInt(result.Metadata.GetValueOrDefault("chunkIndex")),
            result.MatchType,
            highlight.IsHighlightable);
    }

    private static Guid ParseGuid(string? value) =>
        Guid.TryParse(value, out var result) ? result : Guid.Empty;

    private static int? ParseInt(string? value) =>
        int.TryParse(value, out var result) ? result : null;
}
