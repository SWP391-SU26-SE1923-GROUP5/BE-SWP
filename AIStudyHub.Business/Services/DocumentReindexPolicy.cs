using AIStudyHub.Business.AI;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;

namespace AIStudyHub.Business.Services;

public static class DocumentReindexPolicy
{
    public static bool IsEligible(Document document, DateTime now, TimeSpan claimTimeout, int maxAttempts) =>
        document.Status == DocumentStatus.Done
        && document.ProcessingVersion < DocumentIngestionVersion.Current
        && document.LifecycleStatus == DocumentLifecycleStatus.Active
        && !string.IsNullOrWhiteSpace(document.FileLink)
        && IsSupportedFileName(document.FileName)
        && document.ReindexAttemptCount < maxAttempts
        && (document.ReindexClaimId == null
            || document.ReindexClaimedAt == null
            || document.ReindexClaimedAt < now - claimTimeout);

    public static bool IsSupportedFileName(string? fileName) =>
        DocumentRagFilePolicy.SupportsChat(fileName);
}
