namespace AIStudyHub.Business.Interfaces.AI.Search;

public interface IHybridSearchService
{
    Task<IEnumerable<SearchResult>> SearchAsync(string query, Guid userId, int topK = 5, CancellationToken ct = default);
}
