using AIStudyHub.Business.Enums;

namespace AIStudyHub.Business.DTOs.Documents;

public sealed record ExtractedTextSegment(
    string Text,
    DocumentContentType ContentType,
    int? PageNumber);
