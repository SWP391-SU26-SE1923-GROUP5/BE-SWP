using AIStudyHub.Business.AI.Orchestration;
using AIStudyHub.Business.Interfaces.AI.Search;

namespace AIStudyHub.Tests.Services;

public sealed class StructuredExhaustiveAnswerBuilderTests
{
    [Fact]
    public void TryBuild_PdfChunks_ReturnsEveryRuleOnceInNumericOrderWithPhysicalPages()
    {
        SearchResult[] results =
        [
            Result("8 | Page 3. Business Rules BR-01 First rule. BR-02 Second rule.", "srs.pdf", 8, 15),
            Result("BR-02 Second rule. BR-03 Third rule.", "srs.pdf", 9, 16)
        ];

        var built = StructuredExhaustiveAnswerBuilder.TryBuild(
            "Liệt kê toàn bộ business rules", results, out var answer);

        Assert.True(built);
        Assert.Equal(1, Count(answer, "BR-01"));
        Assert.Equal(1, Count(answer, "BR-02"));
        Assert.Equal(1, Count(answer, "BR-03"));
        Assert.True(answer.IndexOf("BR-01", StringComparison.Ordinal) < answer.IndexOf("BR-03", StringComparison.Ordinal));
        Assert.Contains("trang 8", answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("trang 9", answer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AUTHORITATIVE_CITATION_PAGE", answer);
    }

    [Fact]
    public void TryBuild_DocxWithoutPage_UsesSourceWithoutPageOrPlaceholder()
    {
        var results = new[]
        {
            Result("BR-01 First rule. BR-02 Second rule.", "srs.docx", null, 4)
        };

        var built = StructuredExhaustiveAnswerBuilder.TryBuild(
            "List all business rules", results, out var answer);

        Assert.True(built);
        Assert.Contains("Nguồn: srs.docx", answer);
        Assert.DoesNotContain("trang", answer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AUTHORITATIVE_CITATION_PAGE", answer);
    }

    [Fact]
    public void TryBuild_NormalQuestion_DoesNotShortCircuitLlm()
    {
        var built = StructuredExhaustiveAnswerBuilder.TryBuild(
            "BR-01 là gì?", [Result("BR-01 First rule.", "srs.pdf", 8, 15)], out var answer);

        Assert.False(built);
        Assert.Equal(string.Empty, answer);
    }

    [Fact]
    public void TryBuild_RuleSplitAcrossChunks_AppendsLeadingContinuation()
    {
        SearchResult[] results =
        [
            Result("BR-26 OTP expires after 3 minutes (configurable via", "srs.pdf", 9, 16),
            Result("OtpOptions.ExpiryMinutes). BR-27 OTP locks after failures.", "srs.pdf", 9, 17)
        ];

        var built = StructuredExhaustiveAnswerBuilder.TryBuild(
            "Liệt kê tất cả business rules", results, out var answer);

        Assert.True(built);
        Assert.Contains("BR-26: OTP expires after 3 minutes (configurable via OtpOptions.ExpiryMinutes).", answer);
        Assert.Contains("BR-27: OTP locks after failures.", answer);
    }

    [Fact]
    public void TryBuild_EightyTwoRulesAcrossPdfPages_DoesNotSkipAnyIdentifier()
    {
        var results = Enumerable.Range(8, 5)
            .Select((page, pageOffset) =>
            {
                var first = pageOffset * 17 + 1;
                var count = Math.Min(17, 82 - first + 1);
                var text = string.Join(" ", Enumerable.Range(first, count)
                    .Select(number => $"BR-{number:D2} Rule number {number}."));
                return Result(text, "srs.pdf", page, 15 + pageOffset);
            })
            .ToArray();

        var built = StructuredExhaustiveAnswerBuilder.TryBuild(
            "Liệt kê toàn bộ business rules", results, out var answer);

        Assert.True(built);
        var ids = System.Text.RegularExpressions.Regex.Matches(answer, @"\bBR-\d{2}\b")
            .Select(match => match.Value)
            .ToList();
        Assert.Equal(82, ids.Count);
        Assert.Equal("BR-01", ids.First());
        Assert.Equal("BR-82", ids.Last());
    }

    private static SearchResult Result(string text, string source, int? page, int chunkIndex)
    {
        var metadata = new Dictionary<string, string>
        {
            ["documentId"] = Guid.Empty.ToString(),
            ["chunkIndex"] = chunkIndex.ToString(),
            ["fileName"] = source
        };
        if (page.HasValue)
            metadata["pageNumber"] = page.Value.ToString();
        return new SearchResult(text, 0.9, source, metadata);
    }

    private static int Count(string value, string search) =>
        (value.Length - value.Replace(search, string.Empty, StringComparison.Ordinal).Length) / search.Length;
}
