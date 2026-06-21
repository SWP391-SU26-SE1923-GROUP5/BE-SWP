using AIStudyHub.Business.Interfaces.AI.VectorStore;
using AIStudyHub.Business.AI.VectorStore;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using static Qdrant.Client.Grpc.Conditions;

namespace AIStudyHub.Business.AI.VectorStore;

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
        (List<uint> Indices, List<float> Values)? sparseVector,
        Dictionary<string, string> metadata)
    {
        try
        {
            var vectorMap = new Dictionary<string, Vector>();
            
            // Default unnamed dense vector
            vectorMap[""] = embedding;
            
            // Named sparse vector
            if (sparseVector.HasValue && sparseVector.Value.Indices.Count > 0)
            {
                vectorMap["sparse-text"] = new Vector 
                { 
                    Sparse = new SparseVector 
                    { 
                        Indices = { sparseVector.Value.Indices }, 
                        Values = { sparseVector.Value.Values } 
                    } 
                };
            }

            var point = new PointStruct
            {
                Id = new PointId { Uuid = id },
                Vectors = new Vectors { Vectors_ = new NamedVectors { Vectors = { vectorMap } } }
            };

            foreach (var kvp in metadata)
            {
                point.Payload[kvp.Key] = kvp.Value;
            }
            if (!point.Payload.ContainsKey("chunkId"))
            {
                point.Payload["chunkId"] = id;
            }

            var points = new List<PointStruct> { point };

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
                var sparseConfig = new SparseVectorConfig();
                sparseConfig.Map.Add("sparse-text", new SparseVectorParams());

                await _client.CreateCollectionAsync(
                    _options.CollectionName,
                    new VectorParams
                    {
                        Size = (ulong)_options.VectorSize,
                        Distance = Distance.Cosine
                    },
                    sparseVectorsConfig: sparseConfig
                );

                _logger.LogInformation("Created Qdrant collection with sparse vector support: {Collection}", _options.CollectionName);
            }
            else
            {
                try 
                {
                    var sparseConfig = new SparseVectorConfig();
                    sparseConfig.Map.Add("sparse-text", new SparseVectorParams());
                    
                    await _client.UpdateCollectionAsync(
                        _options.CollectionName,
                        sparseVectorsConfig: sparseConfig
                    );
                    _logger.LogInformation("Updated Qdrant collection {Collection} with sparse vector config", _options.CollectionName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Could not update Qdrant collection {Collection} with sparse config (may already exist): {Message}", _options.CollectionName, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure Qdrant collection exists");
        }
    }



    public async Task<List<(string Id, float[] Embedding, Dictionary<string, string> Metadata, double Score)>> HybridSearchAsync(
        float[] denseEmbedding,
        (List<uint> Indices, List<float> Values) sparseVector,
        int topK,
        Dictionary<string, string>? filterMetadata = null)
    {
        try
        {
            var filterMust = new List<object>();
            if (filterMetadata != null && filterMetadata.Count > 0)
            {
                foreach (var kvp in filterMetadata)
                {
                    filterMust.Add(new { key = kvp.Key, match = new { value = kvp.Value } });
                }
            }

            var payload = new
            {
                prefetch = new object[]
                {
                    new
                    {
                        query = denseEmbedding,
                        limit = topK * 2,
                        filter = filterMust.Count > 0 ? new { must = filterMust } : null
                    },
                    new
                    {
                        query = new { indices = sparseVector.Indices, values = sparseVector.Values },
                        @using = "sparse-text",
                        limit = topK * 2,
                        filter = filterMust.Count > 0 ? new { must = filterMust } : null
                    }
                },
                query = new { fusion = "rrf" },
                limit = topK,
                with_payload = true
            };

            var uri = new Uri(_options.Url);
            var restUrl = $"{uri.Scheme}://{uri.Host}:6333/collections/{_options.CollectionName}/points/query";
            
            using var client = new System.Net.Http.HttpClient();
            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            var response = await client.PostAsync(restUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync();
                _logger.LogError("Qdrant REST error: {StatusCode} {Error}", response.StatusCode, errorText);
                return new List<(string, float[], Dictionary<string, string>, double)>();
            }
            
            var responseString = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(responseString);
            
            var resultList = new List<(string Id, float[] Embedding, Dictionary<string, string> Metadata, double Score)>();
            
            if (doc.RootElement.TryGetProperty("result", out var resultElement))
            {
                var pointsArray = resultElement;
                if (resultElement.ValueKind == System.Text.Json.JsonValueKind.Object && resultElement.TryGetProperty("points", out var pointsProp))
                {
                    pointsArray = pointsProp;
                }

                if (pointsArray.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var item in pointsArray.EnumerateArray())
                    {
                        var id = item.GetProperty("id").ToString();
                        var score = item.GetProperty("score").GetDouble();
                        var meta = new Dictionary<string, string>();
                        if (item.TryGetProperty("payload", out var payloadElement))
                        {
                            foreach (var prop in payloadElement.EnumerateObject())
                            {
                                meta[prop.Name] = prop.Value.ToString() ?? "";
                            }
                        }
                        resultList.Add((id, Array.Empty<float>(), meta, score));
                    }
                }
            }
            
            return resultList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Qdrant REST hybrid search failed");
            return new List<(string, float[], Dictionary<string, string>, double)>();
        }
    }

    public async Task<List<Dictionary<string, string>>> GetPayloadsByDocumentIdAsync(Guid documentId)
    {
        try
        {
            var payload = new
            {
                filter = new
                {
                    must = new[]
                    {
                        new { key = "documentId", match = new { value = documentId.ToString() } }
                    }
                },
                limit = 1000,
                with_payload = true
            };

            var uri = new Uri(_options.Url);
            var restUrl = $"{uri.Scheme}://{uri.Host}:6333/collections/{_options.CollectionName}/points/scroll";

            using var client = new System.Net.Http.HttpClient();
            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await client.PostAsync(restUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync();
                _logger.LogError("Qdrant REST scroll error: {StatusCode} {Error}", response.StatusCode, errorText);
                return new List<Dictionary<string, string>>();
            }

            var responseString = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(responseString);

            var resultList = new List<Dictionary<string, string>>();

            if (doc.RootElement.TryGetProperty("result", out var resultElement))
            {
                if (resultElement.TryGetProperty("points", out var pointsArray) && pointsArray.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var item in pointsArray.EnumerateArray())
                    {
                        var meta = new Dictionary<string, string>();
                        if (item.TryGetProperty("payload", out var payloadElement))
                        {
                            foreach (var prop in payloadElement.EnumerateObject())
                            {
                                meta[prop.Name] = prop.Value.ToString() ?? "";
                            }
                        }
                        resultList.Add(meta);
                    }
                }
            }

            return resultList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve payloads for document {DocumentId} from Qdrant", documentId);
            return new List<Dictionary<string, string>>();
        }
    }
}
