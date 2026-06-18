using System.Text;
using System.Text.Json;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIStudyHub.Business.Services;

public sealed class VectorStoreService : IVectorStoreService
{
    private readonly HttpClient _httpClient;
    private readonly RagOptions _options;
    private readonly ILogger<VectorStoreService> _logger;

    public VectorStoreService(
        IHttpClientFactory httpClientFactory,
        IOptions<RagOptions> options,
        ILogger<VectorStoreService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("VectorStoreClient");
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> UpsertVectorAsync(string id, float[] embedding, Dictionary<string, string> metadata)
    {
        if (string.IsNullOrEmpty(_options.PineconeApiKey))
        {
            _logger.LogInformation("Pinecone not configured, skipping vector upsert for {Id}", id);
            return id;
        }

        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Api-Key", _options.PineconeApiKey);
            _httpClient.BaseAddress = new Uri($"https://{_options.PineconeEnvironment}.pinecone.io");

            var payload = new
            {
                vectors = new[]
                {
                    new
                    {
                        id,
                        values = embedding,
                        metadata = metadata.ToDictionary(kv => kv.Key, kv => (object)kv.Value)
                    }
                },
                @namespace = ""
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync($"/vectors/upsert", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Pinecone upsert failed: {Error}", error);
            }

            return id;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pinecone upsert failed for {Id}", id);
            return id;
        }
    }

    public async Task<List<(string Id, float[] Embedding, Dictionary<string, string> Metadata, double Score)>> SearchAsync(
        float[] queryEmbedding, int topK, Dictionary<string, string>? filterMetadata = null)
    {
        if (string.IsNullOrEmpty(_options.PineconeApiKey))
        {
            _logger.LogWarning("Pinecone not configured, returning empty search results");
            return new List<(string, float[], Dictionary<string, string>, double)>();
        }

        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Api-Key", _options.PineconeApiKey);
            _httpClient.BaseAddress = new Uri($"https://{_options.PineconeEnvironment}.pinecone.io");

            var payload = new
            {
                vector = queryEmbedding,
                topK,
                includeValues = true,
                includeMetadata = true,
                filter = filterMetadata,
                @namespace = ""
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync($"/query", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Pinecone query failed: {Error}", error);
                return new List<(string, float[], Dictionary<string, string>, double)>();
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var results = new List<(string, float[], Dictionary<string, string>, double)>();

            foreach (var match in doc.RootElement.GetProperty("matches").EnumerateArray())
            {
                var id = match.GetProperty("id").GetString() ?? "";
                var values = match.GetProperty("values").EnumerateArray()
                    .Select(v => v.GetSingle())
                    .ToArray();
                var metadata = new Dictionary<string, string>();

                if (match.TryGetProperty("metadata", out var metaElement))
                {
                    foreach (var prop in metaElement.EnumerateObject())
                    {
                        metadata[prop.Name] = prop.Value.GetString() ?? "";
                    }
                }

                var score = match.TryGetProperty("score", out var scoreElement)
                    ? scoreElement.GetDouble()
                    : 0.0;

                results.Add((id, values, metadata, score));
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pinecone search failed");
            return new List<(string, float[], Dictionary<string, string>, double)>();
        }
    }

    public async Task DeleteVectorAsync(string id)
    {
        if (string.IsNullOrEmpty(_options.PineconeApiKey))
            return;

        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Api-Key", _options.PineconeApiKey);
            _httpClient.BaseAddress = new Uri($"https://{_options.PineconeEnvironment}.pinecone.io");

            var payload = new { ids = new[] { id }, @namespace = "" };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            await _httpClient.PostAsync("/vectors/delete", content);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pinecone delete failed for {Id}", id);
        }
    }

    public async Task DeleteVectorsByDocumentIdAsync(Guid documentId)
    {
        if (string.IsNullOrEmpty(_options.PineconeApiKey))
            return;

        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Api-Key", _options.PineconeApiKey);
            _httpClient.BaseAddress = new Uri($"https://{_options.PineconeEnvironment}.pinecone.io");

            var payload = new
            {
                filter = new { documentId = documentId.ToString() },
                deleteAll = false,
                @namespace = ""
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            await _httpClient.PostAsync("/vectors/delete", content);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pinecone bulk delete failed for document {DocumentId}", documentId);
        }
    }
}
