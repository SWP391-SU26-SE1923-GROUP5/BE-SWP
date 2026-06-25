namespace AIStudyHub.Business.DTOs.AIChat;

public sealed record ChatSessionResponseDto(Guid Id, Guid UserId, Guid? DocumentId, string SessionTitle, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record CreateChatSessionRequestDto(string SessionTitle);

public sealed record ChatMessageResponseDto(Guid Id, Guid ChatSessionId, string Sender, string Content, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record CreateChatMessageRequestDto(Guid? SessionId, Guid? DocumentId, string Message);
