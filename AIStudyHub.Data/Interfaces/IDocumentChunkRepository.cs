using AIStudyHub.Data.Entities;

namespace AIStudyHub.Data.Interfaces;

public interface IDocumentChunkRepository : IRepository<DocumentChunk>
{
    Task<List<DocumentChunk>> SemanticSearchAsync(
        float[] queryVector,
        int topK = 5,
        Guid? userId = null,
        Guid? subjectId = null,
        CancellationToken cancellationToken = default);
}
