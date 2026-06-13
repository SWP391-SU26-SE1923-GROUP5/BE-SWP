namespace AIStudyHub.Business.DTOs.Rag;

public sealed record UploadDocumentResponseDto(
    Guid DocumentId,
    string Status,
    int ChunkCount,
    string? Message);
