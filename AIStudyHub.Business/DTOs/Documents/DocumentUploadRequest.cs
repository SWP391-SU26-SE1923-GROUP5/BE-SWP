namespace AIStudyHub.Business.DTOs.Documents;

public sealed record DocumentUploadRequest(
    Guid UserId,
    Guid SubjectId,
    string Title,
    string FileName,
    string ContentType,
    long ContentLength,
    Stream Content);
