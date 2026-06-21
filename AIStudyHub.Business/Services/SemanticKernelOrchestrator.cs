using AIStudyHub.Business.Configuration;
using AIStudyHub.Business.Guardrails;
using AIStudyHub.Business.Search;
using AIStudyHub.Business.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIStudyHub.Business.Services;

public interface ISemanticKernelOrchestrator
{
    Task<RagResponse> AskAsync(Guid userId, string question, CancellationToken ct = default);
}

public record RagResponse(
    string Answer,
    List<CitationInfo> Citations,
    double Confidence
);

public record CitationInfo(
    string Source,
    string Content,
    double Relevance
);

public class SemanticKernelOrchestrator : ISemanticKernelOrchestrator
{
    private readonly IKernelMemoryService _kernelMemory;
    private readonly IHybridSearchService _searchService;
    private readonly IRerankingService _rerankingService;
    private readonly IFaithfulnessFilter _faithfulnessFilter;
    private readonly IGroundingVerifier _groundingVerifier;
    private readonly IConfidenceScorer _confidenceScorer;
    private readonly SemanticKernelOptions _options;
    private readonly ILogger<SemanticKernelOrchestrator> _logger;

    public SemanticKernelOrchestrator(
        IKernelMemoryService kernelMemory,
        IHybridSearchService searchService,
        IRerankingService rerankingService,
        IFaithfulnessFilter faithfulnessFilter,
        IGroundingVerifier groundingVerifier,
        IConfidenceScorer confidenceScorer,
        IOptions<SemanticKernelOptions> options,
        ILogger<SemanticKernelOrchestrator> logger)
    {
        _kernelMemory = kernelMemory;
        _searchService = searchService;
        _rerankingService = rerankingService;
        _faithfulnessFilter = faithfulnessFilter;
        _groundingVerifier = groundingVerifier;
        _confidenceScorer = confidenceScorer;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<RagResponse> AskAsync(Guid userId, string question, CancellationToken ct = default)
    {
        _logger.LogInformation("Processing RAG query for user {UserId}", userId);

        // L3: Retrieval with hybrid search and reranking
        var searchResults = await _searchService.SearchAsync(question, userId, 10, ct);
        var rerankedResults = await _rerankingService.RerankAsync(question, searchResults, 5, ct);
        
        var resultList = rerankedResults.ToList();
        if (!resultList.Any())
        {
            return new RagResponse("I couldn't find relevant information in your documents.", new(), 0.0);
        }

        // L4: Generate answer using Kernel Memory Ask
        var answerResult = await _kernelMemory.AskAsync(question, userId, ct);
        var answer = answerResult.Result;

        // L5: Guardrails
        var isFaithful = await _faithfulnessFilter.ValidateAsync(answer, resultList.Select(r => r.Content));
        var groundingResult = await _groundingVerifier.VerifyAsync(answer, resultList);
        var confidence = _confidenceScorer.Score(answer, groundingResult, isFaithful);

        // Build citations
        var citations = resultList.Select((r, i) => new CitationInfo(
            Source: r.Source,
            Content: r.Content,
            Relevance: r.Score
        )).ToList();

        return new RagResponse(answer, citations, confidence);
    }
}
