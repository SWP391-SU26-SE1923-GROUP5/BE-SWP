namespace AIStudyHub.Business.DTOs.Rag;

public sealed record CitationDto(
    int Index,
    string DocumentTitle,
    string ChunkPreview);

public sealed record ReferenceDto(
    int Index,
    Guid DocumentId,
    string DocumentTitle,
    string? PageInfo,
    string ChunkExcerpt);

public sealed record NeighborDto(
    string DocumentTitle,
    double Score,
    string RelevanceLabel);

public sealed record RagChatResponseDto(
    string Answer,
    List<CitationDto> Citations,
    List<ReferenceDto> References,
    List<NeighborDto> Neighbors,
    string? Summary = null);
