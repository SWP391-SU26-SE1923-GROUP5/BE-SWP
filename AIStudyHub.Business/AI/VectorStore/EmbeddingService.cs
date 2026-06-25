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


}
