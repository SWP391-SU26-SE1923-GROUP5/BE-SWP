using AIStudyHub.Business.DTOs.Documents;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IDocumentService : ICrudService<DocumentResponseDto, CreateDocumentRequestDto, UpdateDocumentRequestDto>
{
}
