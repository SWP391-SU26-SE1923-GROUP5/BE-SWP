using System.Text;
using System.Text.RegularExpressions;
using AIStudyHub.Business.Interfaces.AI.Search;

namespace AIStudyHub.Business.AI.Orchestration;

public static partial class StructuredExhaustiveAnswerBuilder
{
    private sealed record Entry(
        string DocumentKey,
        int DocumentOrder,
        string Id,
        string Prefix,
        int Number,
        string Description,
        string Source,
        int? PageNumber);

    public static bool TryBuild(
        string question,
        IReadOnlyList<SearchResult> results,
        out string answer)
    {
        answer = string.Empty;
        if (!RagContextExpander.IsExhaustiveQuery(question))
            return false;

        var ordered = results
            .Select((result, order) => new
            {
                Result = result,
                Order = order,
                ChunkIndex = ParseInt(result.Metadata.GetValueOrDefault("chunkIndex")) ?? order,
                DocumentKey = result.Metadata.GetValueOrDefault("documentId", result.Source)
            })
            .OrderBy(item => item.Order)
            .ThenBy(item => item.ChunkIndex)
            .ToList();
        var documentOrders = ordered
            .Select(item => item.DocumentKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select((key, index) => (key, index))
            .ToDictionary(item => item.key, item => item.index, StringComparer.OrdinalIgnoreCase);
        var entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        var lastEntryByDocument = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in ordered)
        {
            var matches = StructuredIdRegex().Matches(item.Result.Content).ToList();
            if (matches.Count == 0)
                continue;

            var leadingContinuation = CleanDescription(item.Result.Content[..matches[0].Index]);
            if (!string.IsNullOrWhiteSpace(leadingContinuation)
                && lastEntryByDocument.TryGetValue(item.DocumentKey, out var previousKey)
                && entries.TryGetValue(previousKey, out var previous)
                && !previous.Description.EndsWith(leadingContinuation, StringComparison.OrdinalIgnoreCase))
            {
                entries[previousKey] = previous with
                {
                    Description = $"{previous.Description.TrimEnd()} {leadingContinuation}".Trim()
                };
            }

            for (var index = 0; index < matches.Count; index++)
            {
                var match = matches[index];
                var end = index + 1 < matches.Count ? matches[index + 1].Index : item.Result.Content.Length;
                var description = CleanDescription(
                    item.Result.Content[(match.Index + match.Length)..end]);
                if (string.IsNullOrWhiteSpace(description))
                    continue;

                var id = match.Value.ToUpperInvariant();
                var prefix = match.Groups[1].Value.ToUpperInvariant();
                var number = ParseInt(match.Groups[2].Value) ?? int.MaxValue;
                var key = $"{item.DocumentKey}|{id}";
                var candidate = new Entry(
                    item.DocumentKey,
                    documentOrders[item.DocumentKey],
                    id,
                    prefix,
                    number,
                    description,
                    item.Result.Source,
                    ParseInt(item.Result.Metadata.GetValueOrDefault("pageNumber")));

                if (!entries.TryGetValue(key, out var current)
                    || candidate.Description.Length > current.Description.Length)
                    entries[key] = candidate;
                lastEntryByDocument[item.DocumentKey] = key;
            }
        }

        if (entries.Count == 0)
            return false;

        var sorted = entries.Values
            .OrderBy(entry => entry.DocumentOrder)
            .ThenBy(entry => entry.Prefix, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Number)
            .ToList();
        var output = new StringBuilder("Dưới đây là toàn bộ các mục có cấu trúc được trích nguyên văn từ nguồn:\n");
        string? currentHeader = null;

        foreach (var entry in sorted)
        {
            var header = entry.PageNumber.HasValue
                ? $"Nguồn: {entry.Source}, trang {entry.PageNumber.Value}"
                : $"Nguồn: {entry.Source}";
            if (!string.Equals(header, currentHeader, StringComparison.Ordinal))
            {
                output.AppendLine();
                output.AppendLine(header + ":");
                currentHeader = header;
            }
            output.AppendLine($"- {entry.Id}: {entry.Description}");
        }

        answer = output.ToString().Trim();
        return true;
    }

    private static string CleanDescription(string value)
    {
        value = Regex.Replace(value, @"^\s*\d+\s*\|\s*Page\s+", "", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, @"\s+", " ").Trim();
        return value.TrimStart(':', '-', '–', '—', ' ');
    }

    private static int? ParseInt(string? value) =>
        int.TryParse(value, out var number) ? number : null;

    [GeneratedRegex(@"\b([A-Z]{2,10})-(\d{1,4})\b", RegexOptions.IgnoreCase)]
    private static partial Regex StructuredIdRegex();
}
