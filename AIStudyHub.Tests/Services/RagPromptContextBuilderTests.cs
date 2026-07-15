using AIStudyHub.Business.AI.Orchestration;
using AIStudyHub.Business.Interfaces.AI.Search;

namespace AIStudyHub.Tests.Services;

public sealed class RagPromptContextBuilderTests
{
    [Fact]
    public void Build_LabelsPhysicalPdfPageAsAuthoritative()
    {
        var result = new SearchResult(
            "3. Business Rules BR-01...", 0.9, "srs.pdf",
            new Dictionary<string, string> { ["pageNumber"] = "8" });

        var context = RagPromptContextBuilder.Build([result]);

        Assert.Contains("PDF_PHYSICAL_PAGE: 8", context);
        Assert.Contains("AUTHORITATIVE_CITATION_PAGE: 8", context);
        Assert.Contains("3. Business Rules BR-01", context);
    }

    [Fact]
    public void Build_DocxWithoutPage_ExplicitlyDisablesPageCitation()
    {
        var result = new SearchResult(
            "BR-01...", 0.9, "srs.docx", new Dictionary<string, string>());

        var context = RagPromptContextBuilder.Build([result]);

        Assert.Contains("PAGE_CITATION_AVAILABLE: false", context);
        Assert.DoesNotContain("AUTHORITATIVE_CITATION_PAGE", context);
    }
}
