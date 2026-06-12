namespace AIStudyHub.Business.DTOs.Rag;

public sealed record RagChatRequestDto(
    string Message,
    Guid? SessionId,
    bool IncludeDocuments = true,
    List<Guid>? DocumentIds = null);
