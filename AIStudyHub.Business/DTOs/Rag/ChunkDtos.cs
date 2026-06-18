namespace AIStudyHub.Business.DTOs.Rag;

public sealed record ChunkDto(
    Guid Id,
    Guid DocumentId,
    string Content,
    int OrderIndex,
    string? VectorId,
    double Score = 0.0);
