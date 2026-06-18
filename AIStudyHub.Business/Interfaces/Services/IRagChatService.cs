using AIStudyHub.Business.DTOs.Rag;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IRagChatService
{
    Task<RagChatResponseDto> ChatAsync(RagChatRequestDto request, Guid userId);

    /// <summary>
    /// Send a raw prompt to the local LLM, optionally at a custom temperature.
    /// Returns the model's response text without citation formatting.
    /// </summary>
    Task<string> SendRawPromptAsync(string prompt, float temperature = 0.2f);

    /// <summary>
    /// Summarizes a document by retrieving all its chunks and condensing them
    /// into a coherent summary.
    /// </summary>
    Task<string> SummarizeAsync(Guid documentId, Guid userId);
}
