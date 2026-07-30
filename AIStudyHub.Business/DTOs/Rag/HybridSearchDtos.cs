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
    string MatchType)
{
    public static bool TryFromSearchResult(
        SearchResult result,
        out HybridSearchResultDto? dto)
    {
        dto = null;
        if (!Guid.TryParse(result.Metadata.GetValueOrDefault("documentId"), out var documentId)
            || documentId == Guid.Empty)
        {
            return false;
        }

        dto = new HybridSearchResultDto(
            result.Content,
            result.Score,
            documentId,
            result.Metadata.GetValueOrDefault("fileName", result.Source),
            ParseInt(result.Metadata.GetValueOrDefault("pageNumber")),
            ParseInt(result.Metadata.GetValueOrDefault("chunkIndex")),
            result.MatchType);
        return true;
    }

    private static int? ParseInt(string? value) =>
        int.TryParse(value, out var result) ? result : null;
}
