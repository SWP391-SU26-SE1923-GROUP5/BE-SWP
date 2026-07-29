using AIStudyHub.Business.DTOs.Documents;
using AIStudyHub.Business.DTOs.Rag;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IDocumentUploadService
{
    Task<UploadDocumentResponseDto> UploadAsync(
        DocumentUploadRequest request,
        CancellationToken cancellationToken = default);

    Task<UploadDocumentResponseDto> ReprocessAsync(
        Guid documentId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
