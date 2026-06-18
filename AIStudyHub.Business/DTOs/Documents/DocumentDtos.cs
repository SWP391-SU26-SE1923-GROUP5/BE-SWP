using AIStudyHub.Data.Enums;

namespace AIStudyHub.Business.DTOs.Documents;

public sealed record DocumentResponseDto(
    Guid Id,
    Guid UserId,
    Guid SubjectId,
    string Title,
    string? FileLink,
    string? FileName,
    string? FileExtension,
    string? FileType,
    string? SharedUsers,
    string ShareStatus,
    DocumentStatus? Status,
    int VoteCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record CreateDocumentRequestDto(
    Guid UserId,
    Guid SubjectId,
    string Title,
    string? FileName,
    string? FileExtension,
    string? FileType,
    string ShareStatus);

public sealed record UpdateDocumentRequestDto(
    string Title,
    string? FileName,
    string? FileExtension,
    string? FileType,
    string ShareStatus);
