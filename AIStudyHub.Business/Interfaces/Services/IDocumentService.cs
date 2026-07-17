using AIStudyHub.Business.DTOs.Documents;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IDocumentService : ICrudService<DocumentResponseDto, CreateDocumentRequestDto, UpdateDocumentRequestDto>
{
    Task<AIStudyHub.Business.DTOs.Common.PagedResultDto<DocumentResponseDto>> GetAllPagedAsync(Guid userId, AIStudyHub.Business.DTOs.Common.PaginationParams @params, Guid? subjectId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentResponseDto>> GetAllByUserIdAsync(Guid userId, string? keyword = null, Guid? subjectId = null, CancellationToken cancellationToken = default);

    Task<string> GetAvailableFileNameAsync(
        Guid userId,
        string fileName,
        Guid? excludeDocumentId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the list of users a document is shared with and updates its share status.
    /// Only the document owner can change its sharing settings.
    /// </summary>
    Task<ShareDocumentResponseDto> ShareDocumentAsync(
        Guid documentId,
        Guid callerId,
        ShareDocumentRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all active documents (lifecycle = Active) visible to the user (owned or shared or public).
    /// Filters out Trashed and Purged documents.
    /// </summary>
    Task<IReadOnlyList<DocumentResponseDto>> GetTrashAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a document to the trash (sets LifecycleStatus = Trashed, TrashedAt, TrashedBy).
    /// Only the owner can trash their own document.
    /// </summary>
    Task TrashAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores a trashed document (sets LifecycleStatus = Active, clears TrashedAt/TrashedBy).
    /// Only the owner can restore their own document.
    /// </summary>
    Task RestoreAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently purges a trashed document (hard delete).
    /// Only the owner can purge. Document must already be in Trashed state.
    /// </summary>
    Task PurgeAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all DocumentShare entries for a document. Only the owner can view this.
    /// </summary>
    Task<DocumentShareListDto> GetSharesAsync(Guid documentId, Guid callerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a specific user's share access to a document. Only the owner can revoke.
    /// </summary>
    Task RevokeShareAsync(Guid documentId, Guid targetUserId, Guid callerId, CancellationToken cancellationToken = default);
}
