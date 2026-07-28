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

        var hasListIntent = Regex.IsMatch(
            question,
            @"\b(liệt\s+kê|kể\s+ra|list|enumerate)\b",
            RegexOptions.IgnoreCase);
        var hasExplicitLimit = Regex.IsMatch(
            question,
            @"\b(liệt\s+kê|kể\s+ra|list|enumerate)\s+(?:top\s+)?\d+\b",
            RegexOptions.IgnoreCase);

        if (hasExplicitLimit)
            return false;

        return hasListIntent || ExhaustivePhrases.Any(phrase =>
            question.Contains(phrase, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<List<SearchResult>> ExpandAsync(
        string question,
        IReadOnlyList<SearchResult> rankedResults,
        IReadOnlyList<Guid>? documentIds,
        int maxChunks)
    {
        var limit = Math.Max(1, maxChunks);
        if (!IsExhaustiveQuery(question))
            return rankedResults.Take(limit).ToList();

        var selectedDocumentIds = documentIds is { Count: > 0 }
            ? documentIds.Distinct().ToList()
            : rankedResults
                .Select(result => GetDocumentId(result.Metadata))
                .Where(documentId => documentId.HasValue)
                .Select(documentId => documentId!.Value)
                .Distinct()
                .ToList();
        var expanded = new List<(int DocumentOrder, int ChunkIndex, SearchResult Result)>();
        var fallbackScore = rankedResults.Count > 0 ? rankedResults.Max(result => result.Score) : 1d;

        for (var documentOrder = 0; documentOrder < selectedDocumentIds.Count; documentOrder++)
        {
            var documentId = selectedDocumentIds[documentOrder];
            var payloads = await _vectorStore.GetPayloadsByDocumentIdAsync(documentId);

            foreach (var payload in payloads)
            {
                var chunkIndex = GetChunkIndex(payload);
                if (!chunkIndex.HasValue)
                    continue;

                var contentType = payload.GetValueOrDefault("contentType");
                if (contentType is "Summary" or "SystemError")
                    continue;

                var content = payload.GetValueOrDefault("text", "");
                if (string.IsNullOrWhiteSpace(content))
                    continue;

                expanded.Add((documentOrder, chunkIndex.Value, new SearchResult(
                    content,
                    fallbackScore,
                    payload.GetValueOrDefault("fileName", string.Empty),
                    payload,
                    "exhaustive")));
            }
        }

        return expanded
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
