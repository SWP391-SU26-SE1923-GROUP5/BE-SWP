namespace AIStudyHub.Business.Interfaces.Services;

public interface IVectorStoreService
{
    Task<string> UpsertVectorAsync(string id, float[] embedding, Dictionary<string, string> metadata);
    Task<List<(string Id, float[] Embedding, Dictionary<string, string> Metadata, double Score)>> SearchAsync(float[] queryEmbedding, int topK, Dictionary<string, string>? filterMetadata = null);
    Task DeleteVectorAsync(string id);
    Task DeleteVectorsByDocumentIdAsync(Guid documentId);
}
