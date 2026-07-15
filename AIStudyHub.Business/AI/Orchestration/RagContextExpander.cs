using AIStudyHub.Business.Interfaces.AI.Search;
using AIStudyHub.Business.Interfaces.AI.VectorStore;
using System.Text.RegularExpressions;

namespace AIStudyHub.Business.AI.Orchestration;

public sealed class RagContextExpander
{
    private static readonly string[] ExhaustivePhrases =
    [
        "toàn bộ", "tất cả", "đầy đủ", "liệt kê hết", "list all", "complete list"
    ];

    private readonly IVectorStoreService _vectorStore;

    public RagContextExpander(IVectorStoreService vectorStore) => _vectorStore = vectorStore;

    public static bool IsExhaustiveQuery(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return false;

        return ExhaustivePhrases.Any(phrase =>
                question.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            || Regex.IsMatch(
                question,
                @"\b(liệt\s+kê|list)\b.*\bbus+iness\s+rules?\b",
                RegexOptions.IgnoreCase);
    }

    public async Task<List<SearchResult>> ExpandAsync(
        string question,
        IReadOnlyList<SearchResult> rankedResults,
        int adjacentWindow,
        int maxChunks)
    {
        var limit = Math.Max(1, maxChunks);
        if (!IsExhaustiveQuery(question))
            return rankedResults.Take(limit).ToList();

        var expanded = new List<(int DocumentOrder, int ChunkIndex, SearchResult Result)>();
        var excludedSeedKeys = new HashSet<string>(StringComparer.Ordinal);
        var documentOrder = 0;

        foreach (var group in rankedResults
            .Select(result => (Result: result, DocumentId: GetDocumentId(result.Metadata)))
            .Where(item => item.DocumentId.HasValue)
            .GroupBy(item => item.DocumentId!.Value))
        {
            var seedIndexes = group
                .Select(item => GetChunkIndex(item.Result.Metadata))
                .Where(index => index.HasValue)
                .Select(index => index!.Value)
                .ToHashSet();
            var seedScore = group.Max(item => item.Result.Score);
            var payloads = await _vectorStore.GetPayloadsByDocumentIdAsync(group.Key);
            var firstChunk = seedIndexes.Min() - Math.Max(0, adjacentWindow);
            var lastChunk = seedIndexes.Max() + Math.Max(0, adjacentWindow);
            var structuredPrefixes = group
                .SelectMany(item => Regex.Matches(
                    item.Result.Content, @"\b([A-Z]{2,10})-\d{1,4}\b", RegexOptions.IgnoreCase)
                    .Select(match => match.Groups[1].Value))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (structuredPrefixes.Count > 0)
            {
                var matchingIndexes = payloads
                    .Where(payload => Regex.Matches(
                            payload.GetValueOrDefault("text", ""),
                            @"\b([A-Z]{2,10})-\d{1,4}\b",
                            RegexOptions.IgnoreCase)
                        .Any(match => structuredPrefixes.Contains(match.Groups[1].Value)))
                    .Select(GetChunkIndex)
                    .Where(index => index.HasValue)
                    .Select(index => index!.Value)
                    .ToList();
                if (matchingIndexes.Count > 0)
                {
                    // A structured exhaustive query should contain the complete structured
                    // section, not unrelated semantic hits before or after that section.
                    firstChunk = matchingIndexes.Min();
                    lastChunk = matchingIndexes.Max();
                    foreach (var seed in group)
                    {
                        var seedIndex = GetChunkIndex(seed.Result.Metadata);
                        if (!seedIndex.HasValue
                            || seedIndex.Value < firstChunk
                            || seedIndex.Value > lastChunk)
                            excludedSeedKeys.Add(BuildKey(seed.Result));
                    }
                }
            }

            foreach (var payload in payloads)
            {
                var chunkIndex = GetChunkIndex(payload);
                if (!chunkIndex.HasValue
                    || chunkIndex.Value < firstChunk
                    || chunkIndex.Value > lastChunk)
                    continue;

                var contentType = payload.GetValueOrDefault("contentType");
                if (contentType is "Summary" or "SystemError")
                    continue;

                var content = payload.GetValueOrDefault("text", "");
                if (string.IsNullOrWhiteSpace(content))
                    continue;

                expanded.Add((documentOrder, chunkIndex.Value, new SearchResult(
                    content,
                    seedIndexes.Contains(chunkIndex.Value) ? seedScore : seedScore * 0.95,
                    payload.GetValueOrDefault("fileName", group.First().Result.Source),
                    payload,
                    seedIndexes.Contains(chunkIndex.Value) ? "semantic" : "adjacent")));
            }

            documentOrder++;
        }

        var expandedKeys = expanded
            .Select(item => BuildKey(item.Result))
            .ToHashSet(StringComparer.Ordinal);
        var withoutDocumentMetadata = rankedResults
            .Where(result => !expandedKeys.Contains(BuildKey(result))
                && !excludedSeedKeys.Contains(BuildKey(result)))
            .Select((result, index) => (DocumentOrder: int.MaxValue, ChunkIndex: index, Result: result));

        return expanded
            .Concat(withoutDocumentMetadata)
            .OrderBy(item => item.DocumentOrder)
            .ThenBy(item => item.ChunkIndex)
            .Select(item => item.Result)
            .DistinctBy(BuildKey)
            .Take(limit)
            .ToList();
    }

    private static Guid? GetDocumentId(Dictionary<string, string> metadata) =>
        metadata.TryGetValue("documentId", out var value) && Guid.TryParse(value, out var id) ? id : null;

    private static int? GetChunkIndex(Dictionary<string, string> metadata) =>
        metadata.TryGetValue("chunkIndex", out var value) && int.TryParse(value, out var index) ? index : null;

    private static string BuildKey(SearchResult result)
    {
        var documentId = result.Metadata.GetValueOrDefault("documentId");
        var chunkIndex = result.Metadata.GetValueOrDefault("chunkIndex");
        return !string.IsNullOrWhiteSpace(documentId) && !string.IsNullOrWhiteSpace(chunkIndex)
            ? $"{documentId}|{chunkIndex}"
            : $"content|{result.Source}|{result.Content}";
    }
}
