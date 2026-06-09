using AIStudyHub.Business.DTOs.AIChat;
using AIStudyHub.Business.Interfaces.Services;

namespace AIStudyHub.Business.Services;

public sealed class AIChatService : IAIChatService
{
    public Task<IReadOnlyList<ChatSessionResponseDto>> GetSessionsAsync()
    {
        throw new NotImplementedException();
    }

    public Task<ChatSessionResponseDto> CreateSessionAsync(CreateChatSessionRequestDto request)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<ChatMessageResponseDto>> GetMessagesAsync(Guid sessionId)
    {
        throw new NotImplementedException();
    }

    public Task<ChatMessageResponseDto> CreateMessageAsync(CreateChatMessageRequestDto request)
    {
        throw new NotImplementedException();
    }
}
