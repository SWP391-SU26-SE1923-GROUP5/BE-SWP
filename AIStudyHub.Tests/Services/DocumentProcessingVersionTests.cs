using AIStudyHub.Data.Entities;

namespace AIStudyHub.Tests.Services;

public sealed class DocumentProcessingVersionTests
{
    [Fact]
    public void NewDocument_HasLegacyVersionAndNoReindexClaim()
    {
        var document = new Document();

        Assert.Equal(1, document.ProcessingVersion);
        Assert.Null(document.ReindexClaimId);
        Assert.Null(document.ReindexClaimedAt);
        Assert.Equal(0, document.ReindexAttemptCount);
        Assert.Null(document.LastReindexError);
    }
}
