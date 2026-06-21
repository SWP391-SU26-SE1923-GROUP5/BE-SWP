using AIStudyHub.Business.Interfaces.AI.Orchestration;
using AIStudyHub.Business.Interfaces.AI.Search;
using AIStudyHub.Business.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIStudyHub.Business.AI.Search;

public class RerankingService : IRerankingService
{
    private readonly RetrievalOptions _options;
    private readonly ILogger<RerankingService> _logger;

    public RerankingService(IOptions<RetrievalOptions> options, ILogger<RerankingService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<IEnumerable<SearchResult>> RerankAsync(
        string query,
        IEnumerable<SearchResult> results,
        int topK = 5,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Reranking {Count} results to top {TopK}", results.Count(), topK);

        var reranked = results
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .Select((r, index) => r with { Score = r.Score * (1.0 - (index * 0.1)) });

        return Task.FromResult(reranked);
    }
}
