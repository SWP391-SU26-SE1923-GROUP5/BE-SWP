using AIStudyHub.Data.Entities;

namespace AIStudyHub.Business.Interfaces.AI.Orchestration;

// Shared record types used by both ISemanticKernelOrchestrator and SemanticKernelOrchestrator.
// Defined here (not in the interface file) to avoid stale-DLL resolution issues.

public record RagResponse(
    string Answer,
    List<CitationInfo> Citations,
    double Confidence,
    bool IsRelevant
);

public record RagResponseWithUsage(
    string Answer,
    List<CitationInfo> Citations,
    double Confidence,
    int InputTokens,
    int OutputTokens,
    bool IsRelevant
);

public record SummarizeResult(
    string Summary,
    int InputTokens,
    int OutputTokens
);

public record CitationInfo(
    Guid DocumentId,
    string Source,
    string Content,
    double Relevance,
    int? PageNumber,
    string MatchType,
    bool IsHighlightable = false,
    string? Reason = "legacy_unclassified",
    int CitationIndex = 0
);
