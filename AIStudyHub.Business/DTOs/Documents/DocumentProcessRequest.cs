namespace AIStudyHub.Business.DTOs.Documents;

public record DocumentProcessRequest(
    Guid DocumentId,
    Guid UserId,
    string FilePath,
    string FileName,
    string ContentType,
    CancellationToken CancellationToken = default
);
