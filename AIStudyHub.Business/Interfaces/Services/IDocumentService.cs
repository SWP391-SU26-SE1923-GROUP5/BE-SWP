using AIStudyHub.Business.DTOs.Documents;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IDocumentService : ICrudService<DocumentResponseDto, CreateDocumentRequestDto, UpdateDocumentRequestDto>
{
    Task<AIStudyHub.Business.DTOs.Common.PagedResultDto<DocumentResponseDto>> GetAllPagedAsync(Guid userId, AIStudyHub.Business.DTOs.Common.PaginationParams @params, Guid? subjectId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentResponseDto>> GetAllByUserIdAsync(Guid userId, string? keyword = null, Guid? subjectId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the list of users a document is shared with and updates its share status.
    /// Only the document owner can change its sharing settings.
    /// </summary>
    Task<ShareDocumentResponseDto> ShareDocumentAsync(
        Guid documentId,
        Guid callerId,
        ShareDocumentRequestDto request,
        CancellationToken cancellationToken = default);
}
