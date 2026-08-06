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
        var cleanAnswer = RemoveIssuedSourceIds(
                AnyAttributionProtocol().Replace(response, string.Empty),
                sourcesById.Keys)
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

    private static string RemoveIssuedSourceIds(
        string answer,
        IEnumerable<string> sourceIds)
    {
        var idPattern = string.Join(
            "|",
            sourceIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(Regex.Escape));
        if (string.IsNullOrEmpty(idPattern))
            return answer;

        var issuedId = $@"(?<![A-Za-z0-9_])(?:{idPattern})(?![A-Za-z0-9_])";
        var withoutMetadata = Regex.Replace(
            answer,
            $@"\bSOURCE_ID(?:\s*[:/]\s*|\s+){issuedId}",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return Regex.Replace(
            withoutMetadata,
            issuedId,
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
