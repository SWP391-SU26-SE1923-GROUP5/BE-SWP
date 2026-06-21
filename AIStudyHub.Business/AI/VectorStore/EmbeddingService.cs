using AIStudyHub.Business.AI.LLM;
using AIStudyHub.Business.AI.VectorStore;
using AIStudyHub.Business.Interfaces.AI.VectorStore;
using AIStudyHub.Business.Interfaces.AI.LLM;
using System.Text;
using System.Text.Json;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIStudyHub.Business.AI.VectorStore;

public sealed class EmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly ILocalAIService _localAIService;
    private readonly RagOptions _options;
    private readonly ILogger<EmbeddingService> _logger;
    private int? _cachedDimension;

    public EmbeddingService(
        IHttpClientFactory httpClientFactory,
        IOptions<RagOptions> options,
        ILocalAIService openAIService,
        ILogger<EmbeddingService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("EmbeddingClient");
        _localAIService = openAIService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        var embeddings = await GenerateEmbeddingsAsync(new List<string> { text });
        return embeddings.FirstOrDefault() ?? throw new InvalidOperationException("Failed to generate embedding");
    }

    public async Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts)
    {
        return await _localAIService.CreateEmbeddingsFromTexts(texts);
    }

    public int GetEmbeddingDimension()
    {
        return 10;
    }

    private async Task<List<float[]>> GenerateOllamaEmbeddingsAsync(List<string> texts)
    {
        var embeddings = new List<float[]>();

        foreach (var text in texts)
        {
            var payload = new
            {
                model = _options.OllamaModel,
                prompt = text
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            var requestUrl = $"{_options.OllamaUrl!.TrimEnd('/')}/api/embeddings";
            var response = await _httpClient.PostAsync(requestUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Ollama embedding failed: {response.StatusCode} - {error}");
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var embeddingArray = doc.RootElement
                .GetProperty("embedding")
                .EnumerateArray()
                .Select(e => e.GetSingle())
                .ToArray();

            if (!_cachedDimension.HasValue)
            {
                _cachedDimension = embeddingArray.Length;
                _logger.LogInformation("Detected Ollama embedding dimension: {Dimension}", _cachedDimension.Value);
            }

            embeddings.Add(embeddingArray);
        }

        return embeddings;
    }

    /*
    private async Task<List<float[]>> GenerateNomicEmbeddingsAsync(List<string> texts)
    {
        var embeddings = new List<float[]>();

        foreach (var text in texts)
        {
            var payload = new
            {
                texts = new[] { text },
                model = _options.NomicEmbedModel,
                task_type = "search_document"
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_options.NomicApiKey}");

            var requestUrl = $"{_options.NomicApiUrl.TrimEnd('/')}/embedding/text";
            var response = await _httpClient.PostAsync(requestUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Nomic embedding failed: {response.StatusCode} - {error}");
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var embeddingArray = doc.RootElement
                .GetProperty("embeddings")[0]
                .EnumerateArray()
                .Select(e => e.GetSingle())
                .ToArray();

            if (!_cachedDimension.HasValue)
            {
                _cachedDimension = embeddingArray.Length;
                _logger.LogInformation("Detected Nomic embedding dimension: {Dimension}", _cachedDimension.Value);
            }

            embeddings.Add(embeddingArray);
        }

        return embeddings;
    }
    */
}
