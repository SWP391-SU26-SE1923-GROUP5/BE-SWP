using AIStudyHub.Business.DTOs.AIChat;
using AIStudyHub.Business.Interfaces.Services;

namespace AIStudyHub.Business.Services;

public sealed class AIChatService : IAIChatService
{
    public Task<IReadOnlyList<ChatSessionResponseDto>> GetSessionsAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<ChatSessionResponseDto> CreateSessionAsync(CreateChatSessionRequestDto request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<ChatMessageResponseDto>> GetMessagesAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<ChatMessageResponseDto> CreateMessageAsync(CreateChatMessageRequestDto request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
