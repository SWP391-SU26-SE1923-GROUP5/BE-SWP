using AIStudyHub.Data.Enums;

namespace AIStudyHub.Business.DTOs.Documents;

public sealed record DocumentResponseDto(Guid Id, Guid UserId, string Title, string Description, string FileUrl, string ContentType, long FileSizeBytes, DocumentStatus Status, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record CreateDocumentRequestDto(Guid UserId, string Title, string Description, string FileUrl, string ContentType, long FileSizeBytes);

public sealed record UpdateDocumentRequestDto(string Title, string Description, DocumentStatus Status);
