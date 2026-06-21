using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using static Qdrant.Client.Grpc.Conditions;

namespace AIStudyHub.Business.Services;

public sealed class QdrantVectorService : IVectorStoreService
{
    private readonly QdrantClient _client;
    private readonly QdrantOptions _options;
    private readonly ILogger<QdrantVectorService> _logger;

    public QdrantVectorService(
        IOptions<QdrantOptions> options,
        ILogger<QdrantVectorService> logger)
    {
        _options = options.Value;
        _logger = logger;

        var uri = new Uri(_options.Url);
        var host = uri.Host;
        _client = new QdrantClient(host, (int)_options.GrpcPort);
    }

    public async Task<string> UpsertVectorAsync(
        string id,
        float[] embedding,
        Dictionary<string, string> metadata)
    {
        try
        {
            var points = new List<PointStruct>
            {
                new PointStruct
                {
                    Id = new PointId { Uuid = id },
                    Vectors = embedding,
                    Payload =
                    {
                        ["documentId"] = metadata.GetValueOrDefault("documentId", ""),
                        ["chunkId"] = metadata.GetValueOrDefault("chunkId", id),
                        ["userId"] = metadata.GetValueOrDefault("userId", ""),
                        ["chunkIndex"] = metadata.GetValueOrDefault("chunkIndex", "0"),
                        ["documentTitle"] = metadata.GetValueOrDefault("documentTitle", "")
                    }
                }
            };

            await _client.UpsertAsync(_options.CollectionName, points);

            _logger.LogDebug("Upserted vector {Id} to Qdrant", id);
            return id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Qdrant upsert failed for {Id}", id);
            return id;
        }
    }

    public async Task<List<(string Id, float[] Embedding, Dictionary<string, string> Metadata, double Score)>> SearchAsync(
        float[] queryEmbedding,
        int topK,
        Dictionary<string, string>? filterMetadata = null)
    {
        try
        {
            Filter? filter = null;
            if (filterMetadata != null && filterMetadata.Count > 0)
            {
                var conditions = filterMetadata.Select(kvp => MatchText(kvp.Key, kvp.Value)).ToArray();
                filter = new Filter { Must = { conditions } };
            }

            var results = await _client.SearchAsync(
                _options.CollectionName,
                queryEmbedding,
                limit: (ulong)topK,
                filter: filter,
                scoreThreshold: 0.0f);

            return results.Select(r => (
                Id: r.Id.Uuid,
                Embedding: Array.Empty<float>(),
                Metadata: r.Payload.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.ToString()),
                Score: (double)r.Score)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Qdrant search failed");
            return new List<(string, float[], Dictionary<string, string>, double)>();
        }
    }

    public async Task DeleteVectorAsync(string id)
    {
        try
        {
            await _client.DeleteAsync(
                _options.CollectionName,
                new Filter { Must = { MatchText("chunkId", id) } });

            _logger.LogDebug("Deleted vector {Id} from Qdrant", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Qdrant delete failed for {Id}", id);
        }
    }

    public async Task DeleteVectorsByDocumentIdAsync(Guid documentId)
    {
        try
        {
            await _client.DeleteAsync(
                _options.CollectionName,
                new Filter { Must = { MatchText("documentId", documentId.ToString()) } });

            _logger.LogInformation("Deleted all vectors for document {DocumentId} from Qdrant", documentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Qdrant delete by documentId failed for {DocumentId}", documentId);
        }
    }

    public async Task EnsureCollectionExistsAsync()
    {
        try
        {
            var exists = await _client.CollectionExistsAsync(_options.CollectionName);
            if (!exists)
            {
                await _client.CreateCollectionAsync(
                    _options.CollectionName,
                    new VectorParams
                    {
                        Size = (ulong)_options.VectorSize,
                        Distance = Distance.Cosine
                    });

                _logger.LogInformation("Created Qdrant collection: {Collection}", _options.CollectionName);
            }
            else
            {
                _logger.LogInformation("Qdrant collection {Collection} already exists", _options.CollectionName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure Qdrant collection exists");
        }
    }
}
