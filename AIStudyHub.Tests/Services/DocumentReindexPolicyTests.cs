using AIStudyHub.Business.Services;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;

namespace AIStudyHub.Tests.Services;

public sealed class DocumentReindexPolicyTests
{
    [Fact]
    public void IsEligible_RequiresLegacyDoneDocumentAndAvailableClaim()
    {
        var now = DateTime.UtcNow;
        var document = new Document
        {
            Status = DocumentStatus.Done,
            ProcessingVersion = 1,
            FileLink = "/uploads/2026/07/a.pdf",
            FileName = "a.pdf"
        };

        Assert.True(DocumentReindexPolicy.IsEligible(document, now, TimeSpan.FromMinutes(30), 3));

        document.ReindexClaimId = Guid.NewGuid();
        document.ReindexClaimedAt = now.AddMinutes(-5);
        Assert.False(DocumentReindexPolicy.IsEligible(document, now, TimeSpan.FromMinutes(30), 3));

        document.ReindexClaimedAt = now.AddMinutes(-31);
        Assert.True(DocumentReindexPolicy.IsEligible(document, now, TimeSpan.FromMinutes(30), 3));

        document.ReindexAttemptCount = 3;
        Assert.False(DocumentReindexPolicy.IsEligible(document, now, TimeSpan.FromMinutes(30), 3));
    }

    [Fact]
    public void IsEligible_RejectsMediaWithoutTextIndexingPipeline()
    {
        var document = new Document
        {
            Status = DocumentStatus.Done,
            ProcessingVersion = 1,
            FileLink = "/uploads/2026/07/lecture.mp4",
            FileName = "lecture.mp4"
        };

        Assert.False(DocumentReindexPolicy.IsEligible(
            document, DateTime.UtcNow, TimeSpan.FromMinutes(30), 3));
    }
}
