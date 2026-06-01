namespace AIStudyHub.Business.DTOs.AIChat;

public sealed record ChatSessionResponseDto(Guid Id, Guid UserId, string Title, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record CreateChatSessionRequestDto(Guid UserId, string Title);

public sealed record ChatMessageResponseDto(Guid Id, Guid ChatSessionId, string Role, string Content, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record CreateChatMessageRequestDto(Guid ChatSessionId, string Role, string Content);
