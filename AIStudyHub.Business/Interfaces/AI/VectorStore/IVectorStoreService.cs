using AIStudyHub.Business.Interfaces.AI.VectorStore;

namespace AIStudyHub.Business.Interfaces.AI.VectorStore;

public interface IVectorStoreService
{
    Task<string> UpsertVectorAsync(string id, float[] embedding, (List<uint> Indices, List<float> Values)? sparseVector, Dictionary<string, string> metadata);
    Task<List<(string Id, float[] Embedding, Dictionary<string, string> Metadata, double Score)>> SearchAsync(float[] queryEmbedding, int topK, Dictionary<string, string>? filterMetadata = null);
    
    // Hybrid Search methods
    Task<List<(string Id, float[] Embedding, Dictionary<string, string> Metadata, double Score)>> HybridSearchAsync(
        float[] denseEmbedding, 
        (List<uint> Indices, List<float> Values) sparseVector, 
        int topK, 
        Dictionary<string, string>? filterMetadata = null);

    Task DeleteVectorAsync(string id);
    Task DeleteVectorsByDocumentIdAsync(Guid documentId);
    Task EnsureCollectionExistsAsync();
    Task<List<Dictionary<string, string>>> GetPayloadsByDocumentIdAsync(Guid documentId);
}
