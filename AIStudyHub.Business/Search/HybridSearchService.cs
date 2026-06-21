using AIStudyHub.Business.Configuration;
using AIStudyHub.Business.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIStudyHub.Business.Search;

public interface IHybridSearchService
{
    Task<IEnumerable<SearchResult>> SearchAsync(string query, Guid userId, int topK = 10, CancellationToken ct = default);
}

public record SearchResult(
    string Content,
    double Score,
    string Source,
    Dictionary<string, string> Metadata
);

public class HybridSearchService : IHybridSearchService
{
    private readonly IKernelMemoryService _kernelMemory;
    private readonly RetrievalOptions _options;
    private readonly ILogger<HybridSearchService> _logger;

    public HybridSearchService(
        IKernelMemoryService kernelMemory,
        IOptions<RetrievalOptions> options,
        ILogger<HybridSearchService> logger)
    {
        _kernelMemory = kernelMemory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IEnumerable<SearchResult>> SearchAsync(
        string query,
        Guid userId,
        int topK = 10,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Performing hybrid search for user {UserId}", userId);

        var citations = await _kernelMemory.SearchAsync(query, userId, topK, ct);

        var results = citations.SelectMany(citation => citation.Partitions.Select(partition => new SearchResult(
            Content: partition.Text,
            Score: partition.Relevance,
            Source: citation.SourceName,
            Metadata: partition.Tags.ToDictionary(t => t.Key, t => string.Join(",", t.Value))
        )));

        return results;
    }
}
