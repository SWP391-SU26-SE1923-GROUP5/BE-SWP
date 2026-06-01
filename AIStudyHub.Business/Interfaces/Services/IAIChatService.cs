using AIStudyHub.Business.DTOs.AIChat;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IAIChatService
{
    Task<IReadOnlyList<ChatSessionResponseDto>> GetSessionsAsync(CancellationToken cancellationToken = default);
    Task<ChatSessionResponseDto> CreateSessionAsync(CreateChatSessionRequestDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatMessageResponseDto>> GetMessagesAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<ChatMessageResponseDto> CreateMessageAsync(CreateChatMessageRequestDto request, CancellationToken cancellationToken = default);
}
