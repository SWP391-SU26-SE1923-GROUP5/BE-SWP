using System.Text.RegularExpressions;

namespace AIStudyHub.Business.AI.Orchestration;

public sealed record RagAttributionResult(
    string Answer,
    IReadOnlyList<RagContextSource> Sources);

public static partial class RagAttributionParser
{
    [GeneratedRegex(
        @"(?:\r?\n)?\[\[USED_SOURCE_IDS:(?<ids>[^\r\n\]]*)\]\]\s*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex FinalAttributionLine();

    [GeneratedRegex(
        @"[ \t]*\[\[USED_SOURCE_IDS:[^\r\n]*(?:\]\])?[ \t]*",
        RegexOptions.CultureInvariant)]
    private static partial Regex AnyAttributionProtocol();

    [GeneratedRegex(@"^S\d{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidSourceId();

    public static RagAttributionResult Parse(
        string response,
        IReadOnlyDictionary<string, RagContextSource> sourcesById)
    {
        if (string.IsNullOrWhiteSpace(response))
            return new RagAttributionResult(string.Empty, []);

        var match = FinalAttributionLine().Match(response);
        var cleanAnswer = AnyAttributionProtocol()
            .Replace(response, string.Empty)
            .TrimEnd();

        if (!match.Success)
            return new RagAttributionResult(cleanAnswer, []);

        var sources = match.Groups["ids"].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries)
            .Where(id => ValidSourceId().IsMatch(id))
            .Distinct(StringComparer.Ordinal)
            .Where(sourcesById.ContainsKey)
            .Select(id => sourcesById[id])
            .ToList();

        return new RagAttributionResult(cleanAnswer, sources);
    }
}
