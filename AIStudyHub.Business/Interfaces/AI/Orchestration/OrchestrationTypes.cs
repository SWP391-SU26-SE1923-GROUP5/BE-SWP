namespace AIStudyHub.Business.Interfaces.AI.Orchestration;

// Shared record types used by both ISemanticKernelOrchestrator and SemanticKernelOrchestrator.
// Defined here (not in the interface file) to avoid stale-DLL resolution issues.

public sealed record RagResponse(
    string Answer,
    double Confidence,
    bool IsRelevant
);

public sealed record RagResponseWithUsage(
    string Answer,
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
