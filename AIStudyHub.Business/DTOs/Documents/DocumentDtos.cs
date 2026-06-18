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

/// <summary>
/// Request body for saving a list of users that a document is shared with.
/// <see cref="ShareStatus"/> may be "private", "shared", or "public".
/// When <see cref="SharedUserIds"/> is empty and <see cref="ShareStatus"/> is not provided,
/// the document falls back to "private".
/// </summary>
public sealed record ShareDocumentRequestDto(
    List<Guid> SharedUserIds,
    string? ShareStatus);

/// <summary>
/// Response payload returned after a share operation. Includes the parsed
/// list of shared user ids along with the updated share status.
/// </summary>
public sealed record ShareDocumentResponseDto(
    Guid DocumentId,
    string ShareStatus,
    IReadOnlyList<Guid> SharedUserIds);
