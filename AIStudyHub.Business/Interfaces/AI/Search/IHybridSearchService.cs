namespace AIStudyHub.Business.Interfaces.AI.Search;

public interface IHybridSearchService
{
    Task<IEnumerable<SearchResult>> SearchAsync(string query, Guid userId, Guid? documentId, int topK = 5, CancellationToken ct = default);
}
