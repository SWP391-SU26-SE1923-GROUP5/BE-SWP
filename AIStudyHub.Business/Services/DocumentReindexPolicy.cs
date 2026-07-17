using AIStudyHub.Business.AI;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;

namespace AIStudyHub.Business.Services;

public static class DocumentReindexPolicy
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".txt", ".md", ".jpg", ".jpeg", ".png", ".webp", ".gif"
    };

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
        !string.IsNullOrWhiteSpace(fileName)
        && SupportedExtensions.Contains(Path.GetExtension(fileName));
}
