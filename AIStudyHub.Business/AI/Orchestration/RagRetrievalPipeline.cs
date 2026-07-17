using AIStudyHub.Business.Configuration;
using AIStudyHub.Business.Interfaces.AI.Search;
using Microsoft.Extensions.Options;

namespace AIStudyHub.Business.AI.Orchestration;

public sealed class RagRetrievalPipeline
{
    private readonly IHybridSearchService _search;
    private readonly RagContextExpander _contextExpander;
    private readonly RetrievalOptions _options;

    public RagRetrievalPipeline(
        IHybridSearchService search,
        RagContextExpander contextExpander,
        IOptions<RetrievalOptions> options)
    {
        _search = search;
        _contextExpander = contextExpander;
        _options = options.Value;
    }

    public async Task<List<SearchResult>> RetrieveAsync(
        string question,
        Guid userId,
        IReadOnlyList<Guid>? documentIds,
        CancellationToken ct)
    {
        var candidates = (await _search.SearchAsync(
            question, userId, documentIds, Math.Max(1, _options.TopK), ct)).ToList();
        var seeds = candidates.Take(Math.Max(1, _options.RerankTopK)).ToList();

        return await _contextExpander.ExpandAsync(
            question,
            seeds,
            documentIds,
            _options.MaxContextChunks);
    }
}
