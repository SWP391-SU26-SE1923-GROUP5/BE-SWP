namespace AIStudyHub.Business.DTOs.AIChat;

public sealed record ChatSessionResponseDto(Guid Id, Guid UserId, string SessionTitle, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record CreateChatSessionRequestDto(string SessionTitle);

public sealed record UpdateChatSessionRequestDto(string SessionTitle);

public sealed record ChatMessageResponseDto(
    Guid Id,
    Guid ChatSessionId,
    string Sender,
    string Content,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    bool IsRelevant);

public sealed record CreateChatMessageRequestDto(Guid? SessionId, string Message);

public sealed record AddDocumentToSessionRequestDto(Guid DocumentId);

public sealed record ChatSessionDocumentResponseDto(Guid ChatSessionId, Guid DocumentId, string Title, string? FileName, DateTime AddedAt);
