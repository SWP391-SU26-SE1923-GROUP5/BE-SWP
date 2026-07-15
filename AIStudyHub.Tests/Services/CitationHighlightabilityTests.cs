using AIStudyHub.Business.AI.Orchestration;

namespace AIStudyHub.Tests.Services;

public sealed class CitationHighlightabilityTests
{
    [Theory]
    [InlineData("verbatim", "true", true, null)]
    [InlineData("summary", "false", false, "synthetic_summary")]
    [InlineData("altText", "false", false, "document_alt_text")]
    [InlineData("ocr", "false", false, "ocr_text")]
    public void FromMetadata_MapsKnownContentType(
        string contentType, string rawHighlightable, bool expected, string? reason)
    {
        var metadata = new Dictionary<string, string>
        {
            ["contentType"] = contentType,
            ["isHighlightable"] = rawHighlightable
        };

        var result = CitationHighlightability.FromMetadata(metadata);

        Assert.Equal(expected, result.IsHighlightable);
        Assert.Equal(reason, result.Reason);
    }

    [Fact]
    public void FromMetadata_MissingMetadata_FailsClosed()
    {
        var result = CitationHighlightability.FromMetadata(new Dictionary<string, string>());

        Assert.False(result.IsHighlightable);
        Assert.Equal("legacy_unclassified", result.Reason);
    }
}
