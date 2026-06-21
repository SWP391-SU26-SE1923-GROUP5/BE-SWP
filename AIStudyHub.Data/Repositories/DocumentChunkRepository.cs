using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AIStudyHub.Data.Repositories;

public class DocumentChunkRepository : GenericRepository<DocumentChunk>, IDocumentChunkRepository
{
    public DocumentChunkRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<List<DocumentChunk>> SemanticSearchAsync(
        float[] queryVector,
        int topK = 5,
        Guid? userId = null,
        Guid? subjectId = null,
        CancellationToken cancellationToken = default)
    {
        // Semantic search is now handled by QdrantVectorService
        // This repository method is kept for compatibility but should not be used for vector search
        var query = DbContext.DocumentChunks
            .Include(c => c.Document)
            .AsQueryable();

        if (userId.HasValue)
        {
            query = query.Where(c => c.Document.UserId == userId.Value);
        }

        if (subjectId.HasValue)
        {
            query = query.Where(c => c.Document.SubjectId == subjectId.Value);
        }

        return await query
            .OrderBy(c => c.OrderIndex)
            .Take(topK)
            .ToListAsync(cancellationToken);
    }
}
