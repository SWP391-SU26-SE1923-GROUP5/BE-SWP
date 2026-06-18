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
/// The <see cref="ShareStatus"/> on the document is derived from <see cref="SharedUserIds"/>:
/// non-empty list → "shared", empty list → "private".
/// </summary>
public sealed record ShareDocumentRequestDto(
    List<Guid> SharedUserIds);

/// <summary>
/// Response payload returned after a share operation. Contains the parsed
/// list of shared user ids. The document's <c>ShareStatus</c> is owned by the
/// general document update flow (PUT /api/Document/{id}) and is not part of
/// this response.
/// </summary>
public sealed record ShareDocumentResponseDto(
    Guid DocumentId,
    IReadOnlyList<Guid> SharedUserIds);
