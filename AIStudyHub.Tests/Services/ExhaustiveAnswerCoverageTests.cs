using AIStudyHub.Business.AI.Orchestration;
using AIStudyHub.Business.Interfaces.AI.Search;

namespace AIStudyHub.Tests.Services;

public sealed class ExhaustiveAnswerCoverageTests
{
    [Fact]
    public void Analyze_ExhaustiveStructuredContext_ReturnsExpectedAndMissingIds()
    {
        SearchResult[] results =
        [
            Result("BR-01 first. BR-02 second."),
            Result("BR-03 third. BR-04 fourth.")
        ];

        var coverage = ExhaustiveAnswerCoverage.Analyze(
            "Liệt kê toàn bộ business rules", results, "BR-01; BR-04");

        Assert.Equal(["BR-01", "BR-02", "BR-03", "BR-04"], coverage.ExpectedIds);
        Assert.Equal(["BR-02", "BR-03"], coverage.MissingIds);
        Assert.Contains("BR-01, BR-02, BR-03, BR-04", coverage.Instruction);
    }

    [Fact]
    public void Analyze_NormalQuestion_DoesNotRequireCoverage()
    {
        var coverage = ExhaustiveAnswerCoverage.Analyze(
            "BR-01 là gì?", [Result("BR-01 first. BR-02 second.")], "BR-01");

        Assert.Empty(coverage.ExpectedIds);
        Assert.Empty(coverage.MissingIds);
        Assert.Equal(string.Empty, coverage.Instruction);
    }

    private static SearchResult Result(string content) =>
        new(content, 0.9, "srs.pdf", new Dictionary<string, string>());
}
