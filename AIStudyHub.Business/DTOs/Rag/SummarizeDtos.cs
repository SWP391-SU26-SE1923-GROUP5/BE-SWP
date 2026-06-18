namespace AIStudyHub.Business.DTOs.Rag;

public sealed record SummarizeRequestDto(
    Guid DocumentId);

public sealed record SummarizeResponseDto(
    string Summary);
