using Microsoft.AspNetCore.Http;
using Swashbuckle.AspNetCore.Annotations;

namespace AIStudyHub.API.DTOs;

public sealed class UploadDocumentFileRequestDto
{
    [SwaggerSchema("File to upload (.pdf, .docx, .txt, .md, .jpg, .png, .mp4, .mp3, etc.)")]
    public IFormFile File { get; set; } = null!;

    [SwaggerSchema("Document title")]
    public string Title { get; set; } = string.Empty;

    [SwaggerSchema("Subject ID")]
    public Guid SubjectId { get; set; }
}
