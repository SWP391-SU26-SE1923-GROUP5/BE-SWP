using AIStudyHub.Data.Entities;
using AIStudyHub.Business.DTOs.AI;

namespace AIStudyHub.Business.Interfaces.AI.Orchestration;

public interface ISemanticKernelOrchestrator
{
    Task<RagResponse> AskAsync(Guid userId, Guid? documentId, string question, IReadOnlyList<ChatMessage> history, CancellationToken ct = default);
    Task<RagResponseWithUsage> AskWithTrackingAsync(Guid userId, Guid? documentId, string question, IReadOnlyList<ChatMessage> history, CancellationToken ct = default);
    Task<string> SummarizeAsync(Guid documentId, Guid userId, CancellationToken ct = default);
    Task<SummarizeResult> SummarizeWithTrackingAsync(Guid documentId, Guid userId, CancellationToken ct = default);
}

public record RagResponse(
    string Answer,
    List<CitationInfo> Citations,
    double Confidence
);

public record RagResponseWithUsage(
    string Answer,
    List<CitationInfo> Citations,
    double Confidence,
    int InputTokens,
    int OutputTokens
);

public record SummarizeResult(
    string Summary,
    int InputTokens,
    int OutputTokens
);

public record CitationInfo(
    string Source,
    string Content,
    double Relevance
);
