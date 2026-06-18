using AIStudyHub.Business.DTOs.Documents;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IDocumentService : ICrudService<DocumentResponseDto, CreateDocumentRequestDto, UpdateDocumentRequestDto>
{
    Task<IReadOnlyList<DocumentResponseDto>> GetAllByUserIdAsync(Guid userId, string? keyword = null, Guid? subjectId = null, CancellationToken cancellationToken = default);
}
