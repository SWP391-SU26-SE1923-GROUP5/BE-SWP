namespace AIStudyHub.Business.DTOs.Rag;

public sealed record UploadDocumentRequestDto(
    string Title,
    Guid SubjectId,
    string FileBase64,
    string FileName,
    string FileExtension);
