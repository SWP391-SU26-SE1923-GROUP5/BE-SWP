using AIStudyHub.Business.DTOs.Rag;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IRagChatService
{
    Task<RagChatResponseDto> ChatAsync(RagChatRequestDto request, Guid userId);
}
