using AIStudyHub.Business.DTOs.AIChat;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IAIChatService
{
    Task<IReadOnlyList<ChatSessionResponseDto>> GetSessionsAsync();
    Task<ChatSessionResponseDto> CreateSessionAsync(CreateChatSessionRequestDto request);
    Task<IReadOnlyList<ChatMessageResponseDto>> GetMessagesAsync(Guid sessionId);
    Task<ChatMessageResponseDto> CreateMessageAsync(CreateChatMessageRequestDto request);
}
