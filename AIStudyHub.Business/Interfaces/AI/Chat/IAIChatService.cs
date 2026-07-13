using AIStudyHub.Business.DTOs.AIChat;
using AIStudyHub.Business.Interfaces.AI.Chat;
namespace AIStudyHub.Business.Interfaces.AI.Chat;

public interface IAIChatService
{
    Task<IReadOnlyList<ChatSessionResponseDto>> GetSessionsAsync();
    Task<ChatSessionResponseDto> CreateSessionAsync(CreateChatSessionRequestDto request, Guid userId);
    Task<IReadOnlyList<ChatMessageResponseDto>> GetMessagesAsync(Guid sessionId);
    Task<ChatMessageResponseDto> CreateMessageAsync(CreateChatMessageRequestDto request, Guid userId, CancellationToken ct = default);
    Task<ChatSessionDocumentResponseDto> AddDocumentAsync(Guid sessionId, Guid documentId, Guid userId, CancellationToken ct = default);
    Task RemoveDocumentAsync(Guid sessionId, Guid documentId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<ChatSessionDocumentResponseDto>> GetDocumentsAsync(Guid sessionId, Guid userId, CancellationToken ct = default);
}
