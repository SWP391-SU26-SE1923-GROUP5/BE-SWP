using AIStudyHub.Business.Interfaces.AI.Orchestration;
using AIStudyHub.Business.Interfaces.AI.Search;
using AIStudyHub.Business.Interfaces.AI.VectorStore;
using AIStudyHub.Business.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIStudyHub.Business.AI.Search;

public class HybridSearchService : IHybridSearchService
{
    private readonly IVectorStoreService _vectorStore;
    private readonly IEmbeddingService _embeddingService;
    private readonly ISparseVectorGenerator _sparseGenerator;
    private readonly IRerankingService _rerankingService;
    private readonly RetrievalOptions _options;
    private readonly ILogger<HybridSearchService> _logger;

    public HybridSearchService(
        IVectorStoreService vectorStore,
        IEmbeddingService embeddingService,
        ISparseVectorGenerator sparseGenerator,
        IRerankingService rerankingService,
        IOptions<RetrievalOptions> options,
        ILogger<HybridSearchService> logger)
    {
        _vectorStore = vectorStore;
        _embeddingService = embeddingService;
        _sparseGenerator = sparseGenerator;
        _rerankingService = rerankingService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IEnumerable<SearchResult>> SearchAsync(
        string query,
        Guid userId,
        IReadOnlyList<Guid>? documentIds,
        int topK = 10,
        CancellationToken ct = default)
    {
        _logger.LogInformation("HybridSearch START: query='{Query}', userId={UserId}, documentIds={DocumentIds}", query, userId, documentIds?.Count);

        // 1. Generate query representations
        var denseEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);
        var sparseVector = _sparseGenerator.GenerateSparseVector(query);

        // 2. Build filter: userId always required; documentIds becomes a MatchAny over documentId field
        var filter = new Dictionary<string, string> { { "userId", userId.ToString() } };
        _logger.LogInformation("HybridSearch: Calling Qdrant with filter={Filter} and documentIds={DocIds}", string.Join(",", filter.Select(kv => $"{kv.Key}={kv.Value}")), documentIds?.Count ?? 0);

        var qdrantResults = await _vectorStore.HybridSearchAsync(denseEmbedding, sparseVector, topK, filter, documentIds);
        _logger.LogInformation("HybridSearch: Qdrant returned {Count} results", qdrantResults.Count);

        // Map to SearchResult
        var results = qdrantResults.Select(r => new SearchResult(
            Content: r.Metadata.GetValueOrDefault("text", ""),
            Score: r.Score,
            Source: r.Metadata.GetValueOrDefault("fileName", "Unknown"),
            Metadata: r.Metadata
        )).ToList();

        _logger.LogInformation("Search query: '{Query}' | Results: {Sources}",
            query, string.Join(" | ", results.Select(r => r.Source)));

        // 3. Rerank the fused results
        var rerankedResults = await _rerankingService.RerankAsync(query, results, topK, ct);
        _logger.LogInformation("HybridSearch: After rerank: {Count} results | Sources: {Sources}",
            rerankedResults.Count(), string.Join(" | ", rerankedResults.Select(r => r.Source)));

        // DEBUG: Log chunk content preview
        var debugIndex = 0;
        foreach (var r in rerankedResults)
        {
            var preview = r.Content.Length > 300 ? r.Content[..300] + "..." : r.Content;
            preview = preview.Replace("\n", "\\n");
            Console.WriteLine($"[DEBUG-CHUNK-{debugIndex++}] Source={r.Source} Score={r.Score:F4} ContentLen={r.Content.Length}");
            Console.WriteLine($"[DEBUG-CHUNK-{debugIndex - 1}-PREVIEW] {preview}");
        }

        return rerankedResults;
    }
}
