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
        Guid? documentId,
        int topK = 10,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Performing true hybrid search for user {UserId} with query: {Query}", userId, query);

        // 1. Generate query representations
        var denseEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);
        var sparseVector = _sparseGenerator.GenerateSparseVector(query);

        // 2. Search in Qdrant using RRF
        var filter = new Dictionary<string, string> { { "userId", userId.ToString() } };
        if (documentId.HasValue)
        {
            filter.Add("documentId", documentId.Value.ToString());
        }
        var qdrantResults = await _vectorStore.HybridSearchAsync(denseEmbedding, sparseVector, topK * 2, filter);

        // Map to SearchResult
        var results = qdrantResults.Select(r => new SearchResult(
            Content: r.Metadata.GetValueOrDefault("text", ""),
            Score: r.Score,
            Source: r.Metadata.GetValueOrDefault("fileName", "Unknown"),
            Metadata: r.Metadata
        )).ToList();

        // 3. Rerank the fused results
        var rerankedResults = await _rerankingService.RerankAsync(query, results, topK, ct);

        return rerankedResults;
    }
}
