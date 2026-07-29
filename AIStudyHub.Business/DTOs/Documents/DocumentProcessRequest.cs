namespace AIStudyHub.Business.DTOs.Documents;

public record DocumentProcessRequest(
    Guid DocumentId,
    Guid UserId,
    string FilePath,
    string FileName,
    string ContentType,
    CancellationToken CancellationToken = default,
    Guid? IndexRunId = null,
    bool IsReindex = false,
    Guid? ReindexClaimId = null,
    bool IsRecovery = false,
    bool IsReprocess = false
);
