namespace AIStudyHub.Business.Interfaces.AI.Search;

public interface IRerankingService
{
    Task<IEnumerable<SearchResult>> RerankAsync(string query, IEnumerable<SearchResult> documents, int topK = 5, CancellationToken ct = default);
}
