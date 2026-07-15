using System.Text.RegularExpressions;
using AIStudyHub.Business.Interfaces.AI.Search;

namespace AIStudyHub.Business.AI.Orchestration;

public sealed record ExhaustiveCoverageResult(
    IReadOnlyList<string> ExpectedIds,
    IReadOnlyList<string> MissingIds,
    string Instruction);

public static partial class ExhaustiveAnswerCoverage
{
    public static ExhaustiveCoverageResult Analyze(
        string question,
        IEnumerable<SearchResult> results,
        string answer)
    {
        if (!RagContextExpander.IsExhaustiveQuery(question))
            return new ExhaustiveCoverageResult([], [], string.Empty);

        var expected = results
            .SelectMany(result => StructuredIdRegex().Matches(result.Content).Select(match => match.Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(GetPrefix, StringComparer.OrdinalIgnoreCase)
            .ThenBy(GetNumber)
            .ToList();

        if (expected.Count == 0)
            return new ExhaustiveCoverageResult([], [], string.Empty);

        var answerIds = StructuredIdRegex().Matches(answer)
            .Select(match => match.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = expected.Where(id => !answerIds.Contains(id)).ToList();
        var instruction = $"EXHAUSTIVE_REQUIRED_IDS: {string.Join(", ", expected)}\n"
            + "You must include every ID above exactly once, in ascending order. Do not sample, omit ranges, or claim completeness while any ID is missing.";

        return new ExhaustiveCoverageResult(expected, missing, instruction);
    }

    private static string GetPrefix(string value) => value[..value.LastIndexOf('-')];

    private static int GetNumber(string value) =>
        int.TryParse(value[(value.LastIndexOf('-') + 1)..], out var number) ? number : int.MaxValue;

    [GeneratedRegex(@"\b[A-Z]{2,10}-\d{1,4}\b", RegexOptions.IgnoreCase)]
    private static partial Regex StructuredIdRegex();
}
