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
    long FileSizeBytes,
    string ShareStatus,
    DocumentStatus? Status,
    string? ErrorMessage,
    int VoteCount,
    DocumentLifecycleStatus LifecycleStatus,
    DateTime? TrashedAt,
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
/// The <c>ShareStatus</c> on the document is derived from <c>SharedUserIds</c>:
/// non-empty list → "shared", empty list → "private".
/// </summary>
public sealed record ShareDocumentRequestDto(
    List<Guid> SharedUserIds,
    /// <summary>
    /// Optional per-user share levels. If provided, must have the same count and order as SharedUserIds.
    /// If null, defaults to Read for all users.
    /// </summary>
    List<int>? Levels);

/// <summary>
/// Response payload returned after a share operation. Contains the parsed
/// list of shared user ids with their share levels. The document's <c>ShareStatus</c> is owned by the
/// general document update flow (PUT /api/Document/{id}) and is not part of
/// this response.
/// </summary>
public sealed record ShareDocumentResponseDto(
    Guid DocumentId,
    IReadOnlyList<Guid> SharedUserIds,
    IReadOnlyList<int> Levels);

/// <summary>
/// Minimal DTO for a single DocumentShare entry returned in list responses.
/// </summary>
public sealed record DocumentShareDto(
    Guid ShareId,
    Guid DocumentId,
    Guid UserId,
    string? UserFullName,
    ShareLevel Level,
    DateTime SharedAt);

/// <summary>
/// Response for GET /api/Document/{id}/shares
/// </summary>
public sealed record DocumentShareListDto(
    Guid DocumentId,
    IReadOnlyList<DocumentShareDto> Shares);

/// <summary>
/// DTO for trash-bin operations.
/// </summary>
public sealed record TrashBinDto(
    Guid DocumentId,
    string Title,
    DocumentLifecycleStatus LifecycleStatus,
    DateTime? TrashedAt,
    Guid? TrashedBy,
    DateTime CreatedAt);

/// <summary>
/// Request to permanently purge a trashed document.
/// </summary>
public sealed record PurgeDocumentRequestDto(bool ConfirmPurge = true);
