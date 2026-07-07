using AIStudyHub.Business.Interfaces.AI.LLM;
using AIStudyHub.Business.Interfaces.AI.VectorStore;
using Microsoft.Extensions.Logging;

namespace AIStudyHub.Business.AI.VectorStore;

public sealed class EmbeddingService : IEmbeddingService
{
    private readonly IOpenAIService _openAIService;
    private readonly ILogger<EmbeddingService> _logger;

    public EmbeddingService(
        IOpenAIService openAIService,
        ILogger<EmbeddingService> logger)
    {
        _openAIService = openAIService;
        _logger = logger;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        var embeddings = await GenerateEmbeddingsAsync(new List<string> { text });
        return embeddings.FirstOrDefault() ?? throw new InvalidOperationException("Failed to generate embedding");
    }

    public async Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts)
    {
        return await _openAIService.CreateEmbeddingsFromTexts(texts);
    }
}
