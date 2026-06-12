using System.Text;
using System.Text.Json;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIStudyHub.Business.Services;

public sealed class EmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly RagOptions _options;
    private readonly ILogger<EmbeddingService> _logger;
    private int? _cachedDimension;

    public EmbeddingService(
        IHttpClientFactory httpClientFactory,
        IOptions<RagOptions> options,
        ILogger<EmbeddingService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("EmbeddingClient");
        _options = options.Value;
        _logger = logger;

        if (_options.UseLocalEmbeddings && !string.IsNullOrEmpty(_options.LocalEmbeddingUrl))
        {
            _httpClient.BaseAddress = new Uri(_options.LocalEmbeddingUrl);
        }
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        var embeddings = await GenerateEmbeddingsAsync(new List<string> { text });
        return embeddings.FirstOrDefault() ?? throw new InvalidOperationException("Failed to generate embedding");
    }

    public async Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts)
    {
        // Priority: Ollama > Local > OpenAI > Simple fallback
        if (!string.IsNullOrEmpty(_options.OllamaModel))
        {
            return await GenerateOllamaEmbeddingsAsync(texts);
        }

        if (_options.UseLocalEmbeddings)
        {
            return await GenerateLocalEmbeddingsAsync(texts);
        }

        if (!string.IsNullOrEmpty(_options.OpenAiApiKey))
        {
            return await GenerateOpenAiEmbeddingsAsync(texts);
        }

        _logger.LogWarning("No embedding provider configured, using simple hash-based fallback");
        return texts.Select(_ => GenerateSimpleEmbedding(_)).ToList();
    }

    public int GetEmbeddingDimension()
    {
        if (_cachedDimension.HasValue)
            return _cachedDimension.Value;

        return _options.OllamaEmbeddingDimension;
    }

    private async Task<List<float[]>> GenerateOllamaEmbeddingsAsync(List<string> texts)
    {
        try
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

                var response = await _httpClient.PostAsync($"{_options.OllamaUrl}/api/embeddings", content);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Ollama embedding failed: {StatusCode}, falling back to simple embeddings", response.StatusCode);
                    return texts.Select(_ => GenerateSimpleEmbedding(_)).ToList();
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                var embedding = doc.RootElement
                    .GetProperty("embedding")
                    .EnumerateArray()
                    .Select(e => e.GetSingle())
                    .ToArray();

                // Auto-detect dimension from first response
                if (!_cachedDimension.HasValue)
                {
                    _cachedDimension = embedding.Length;
                    _logger.LogInformation("Detected Ollama embedding dimension: {Dimension}", _cachedDimension.Value);
                }

                embeddings.Add(embedding);
            }

            return embeddings;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ollama embedding service unavailable, using simple embeddings");
            return texts.Select(_ => GenerateSimpleEmbedding(_)).ToList();
        }
    }

    private async Task<List<float[]>> GenerateLocalEmbeddingsAsync(List<string> texts)
    {
        try
        {
            var payload = new
            {
                input = texts,
                model = _options.LocalEmbeddingModel
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync("/embeddings", content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Local embedding service failed: {StatusCode}, falling back to simple embeddings", response.StatusCode);
                return texts.Select(_ => GenerateSimpleEmbedding(_)).ToList();
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var embeddings = new List<float[]>();

            foreach (var item in doc.RootElement.GetProperty("data").EnumerateArray())
            {
                var embedding = item.GetProperty("embedding").EnumerateArray()
                    .Select(e => e.GetSingle())
                    .ToArray();
                embeddings.Add(embedding);
            }

            return embeddings;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Local embedding service unavailable, using simple embeddings");
            return texts.Select(_ => GenerateSimpleEmbedding(_)).ToList();
        }
    }

    private async Task<List<float[]>> GenerateOpenAiEmbeddingsAsync(List<string> texts)
    {
        var payload = new
        {
            input = texts,
            model = _options.OpenAiEmbeddingModel
        };

        var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_options.OpenAiApiKey}");
        _httpClient.BaseAddress = new Uri("https://api.openai.com");

        var response = await _httpClient.PostAsync("/v1/embeddings", content);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"OpenAI embedding failed: {await response.Content.ReadAsStringAsync()}");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var embeddings = new List<float[]>();

        foreach (var item in doc.RootElement.GetProperty("data").EnumerateArray())
        {
            var embedding = item.GetProperty("embedding").EnumerateArray()
                .Select(e => e.GetSingle())
                .ToArray();
            embeddings.Add(embedding);
        }

        return embeddings;
    }

    private static float[] GenerateSimpleEmbedding(string text)
    {
        var dimension = 384;
        var embedding = new float[dimension];
        var words = text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in words)
        {
            var hash = word.GetHashCode();
            for (var i = 0; i < dimension; i++)
            {
                embedding[i] += (float)Math.Sin(hash * (i + 1) * 0.1) * 0.01f;
            }
        }

        var magnitude = (float)Math.Sqrt(embedding.Sum(e => e * e));
        if (magnitude > 0)
        {
            for (var i = 0; i < dimension; i++)
                embedding[i] /= magnitude;
        }

        return embedding;
    }
}
