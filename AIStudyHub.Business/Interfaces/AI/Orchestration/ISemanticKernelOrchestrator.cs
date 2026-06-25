using AIStudyHub.Data.Entities;

namespace AIStudyHub.Business.Interfaces.AI.Orchestration;

public interface ISemanticKernelOrchestrator
{
    Task<RagResponse> AskAsync(Guid userId, Guid? documentId, string question, IReadOnlyList<ChatMessage> history, CancellationToken ct = default);
    Task<string> SummarizeAsync(Guid documentId, Guid userId, CancellationToken ct = default);
}

public record RagResponse(
    string Answer,
    List<CitationInfo> Citations,
    double Confidence
);

public record CitationInfo(
    string Source,
    string Content,
    double Relevance
);
