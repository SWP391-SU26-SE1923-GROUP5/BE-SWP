using AIStudyHub.Business.DTOs.AIChat;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IAIChatService
{
    Task<IReadOnlyList<ChatSessionResponseDto>> GetSessionsAsync(Guid? userId = null);
    Task<ChatSessionResponseDto> CreateSessionAsync(CreateChatSessionRequestDto request, Guid userId);
    Task<IReadOnlyList<ChatMessageResponseDto>> GetMessagesAsync(Guid sessionId, Guid userId);
    Task<ChatMessageResponseDto> CreateMessageAsync(CreateChatMessageRequestDto request, Guid userId);
}
