using AIStudyHub.Business.Enums;

namespace AIStudyHub.Business.DTOs.Documents;

public class DocumentChunkDto
{
    public required string Text { get; set; }
    public int? PageNumber { get; set; }
    public DocumentContentType ContentType { get; set; } = DocumentContentType.Verbatim;
}
