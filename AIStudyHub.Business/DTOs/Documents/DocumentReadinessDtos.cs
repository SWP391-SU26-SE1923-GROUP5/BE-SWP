namespace AIStudyHub.Business.DTOs.Documents;

public sealed record DocumentReadinessDto(
    string Status,
    bool IsChatReady,
    string Message,
    bool CanRetry);

public sealed record DocumentReadinessStatusResponseDto(
    Guid Id,
    string Status,
    bool IsChatReady,
    string Message,
    bool CanRetry);

public sealed record BlockingDocumentResponseDto(
    Guid DocumentId,
    string Title,
    string Status,
    bool IsChatReady,
    string Message,
    bool CanRetry);
