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
        // Convert float[] to byte[] for SQL Server binary comparison
        var queryVectorBytes = new byte[queryVector.Length * sizeof(float)];
        Buffer.BlockCopy(queryVector, 0, queryVectorBytes, 0, queryVectorBytes.Length);

        // Build query with optional filters
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

        // Order by vector similarity using raw SQL for dot product
        // Note: For true vector similarity, SQL Server 2022+ native vector type is needed
        // This implementation stores vectors as VARBINARY and uses basic ordering
        // For production, consider using OPENAI vector similarity or full-text search
        var results = await query
            .OrderByDescending(c => c.OrderIndex) // Fallback ordering
            .Take(topK)
            .ToListAsync(cancellationToken);

        // Calculate cosine similarity scores for ranking
        if (results.Count > 0)
        {
            var scoredResults = results
                .Where(c => c.Vector != null && c.Vector.Length > 0)
                .Select(c => new
                {
                    Chunk = c,
                    Score = CalculateCosineSimilarity(
                        BytesToFloats(c.Vector),
                        queryVector)
                })
                .OrderByDescending(x => x.Score)
                .Take(topK)
                .Select(x => x.Chunk)
                .ToList();

            return scoredResults;
        }

        return results;
    }

    private static float[] BytesToFloats(byte[] bytes)
    {
        var floats = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }

    private static float CalculateCosineSimilarity(float[] vec1, float[] vec2)
    {
        if (vec1.Length != vec2.Length)
            return 0;

        var dotProduct = 0.0;
        var norm1 = 0.0;
        var norm2 = 0.0;

        for (var i = 0; i < vec1.Length; i++)
        {
            dotProduct += vec1[i] * vec2[i];
            norm1 += vec1[i] * vec1[i];
            norm2 += vec2[i] * vec2[i];
        }

        var denominator = Math.Sqrt(norm1) * Math.Sqrt(norm2);
        return denominator > 0 ? (float)(dotProduct / denominator) : 0;
    }
}
