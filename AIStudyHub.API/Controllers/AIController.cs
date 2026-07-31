using AIStudyHub.Business.DTOs.Flashcards;
using AIStudyHub.Business.DTOs.Quizzes;
using AIStudyHub.Business.DTOs.Rag;
using AIStudyHub.Business.Interfaces.AI.Generators;
using AIStudyHub.Business.Interfaces.AI.Orchestration;
using AIStudyHub.Business.Interfaces.AI.Search;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/AI")]
public sealed class AIController : ControllerBase
{
    private readonly ISemanticKernelOrchestrator _orchestrator;
    private readonly IHybridSearchService _hybridSearchService;
    private readonly RetrievalOptions _retrievalOptions;
    private readonly IFlashcardAiService _flashcardAiService;
    private readonly IQuizAiService _quizAiService;
    private readonly ILogger<AIController> _logger;
    private readonly IRealTimeNotificationService _realTimeNotifier;
    private readonly AIStudyHub.Data.Interfaces.IUnitOfWork _unitOfWork;

    public AIController(
        ISemanticKernelOrchestrator orchestrator,
        IHybridSearchService hybridSearchService,
        IOptions<RetrievalOptions> retrievalOptions,
        IFlashcardAiService flashcardAiService,
        IQuizAiService quizAiService,
        ILogger<AIController> logger,
        IRealTimeNotificationService realTimeNotifier,
        AIStudyHub.Data.Interfaces.IUnitOfWork unitOfWork)
    {
        _orchestrator = orchestrator;
        _hybridSearchService = hybridSearchService;
        _retrievalOptions = retrievalOptions.Value;
        _flashcardAiService = flashcardAiService;
        _quizAiService = quizAiService;
        _logger = logger;
        _realTimeNotifier = realTimeNotifier;
        _unitOfWork = unitOfWork;
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub")
            ?? User.FindFirst("userId");

        return claim != null && Guid.TryParse(claim.Value, out var userId)
            ? userId
            : Guid.Empty;
    }

    /// <summary>Search the user's documents using hybrid dense/sparse retrieval and reranking.</summary>
    [HttpPost("rag/ask")]
    public async Task<ActionResult<HybridSearchResponseDto>> Ask(
        [FromBody] HybridSearchRequestDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest("Question is required");

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var defaultTopK = Math.Max(1, _retrievalOptions.RerankTopK);
        var maxTopK = Math.Max(defaultTopK, _retrievalOptions.TopK);
        var topK = request.TopK ?? defaultTopK;
        if (topK < 1 || topK > maxTopK)
            return BadRequest($"TopK must be between 1 and {maxTopK}");

        var documentIds = request.DocumentIds?
            .Where(documentId => documentId != Guid.Empty)
            .Distinct()
            .ToList();
        if (documentIds is { Count: 0 })
            documentIds = null;

        try
        {
            _logger.LogInformation(
                "Hybrid search from user {UserId}: {Question}; DocumentCount={DocumentCount}; TopK={TopK}",
                userId, request.Question, documentIds?.Count ?? 0, topK);
            var searchResults = await _hybridSearchService.SearchAsync(
                request.Question, userId, documentIds, topK, ct);
            var results = new List<HybridSearchResultDto>();
            foreach (var searchResult in searchResults)
            {
                if (HybridSearchResultDto.TryFromSearchResult(searchResult, out var result))
                {
                    results.Add(result!);
                    continue;
                }

                _logger.LogWarning(
                    "Hybrid search result from source {Source} was omitted because documentId metadata is invalid",
                    searchResult.Source);
            }

            return Ok(new HybridSearchResponseDto(request.Question, results.Count, results));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing hybrid search for user {UserId}", userId);
            return StatusCode(500, "An error occurred while searching your documents");
        }
    }

    /// <summary>Summarize a document using AI.</summary>
    [HttpPost("rag/summarize")]
    public async Task<IActionResult> Summarize([FromBody] SummarizeRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        try
        {
            _logger.LogInformation("Summarize request for document {DocumentId} from user {UserId}", request.DocumentId, userId);
            var summary = await _orchestrator.SummarizeAsync(request.DocumentId, userId, ct);
            return Ok(new { summary });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing summarize request for document {DocumentId}", request.DocumentId);
            return StatusCode(500, "An error occurred while summarizing the document");
        }
    }

    /// <summary>Generate flashcards from a document using AI.</summary>
    [HttpPost("flashcards/generate")]
    public async Task<ActionResult<FlashcardDeckResponseDto>> GenerateFlashcards(
        Guid docId,
        [FromBody] CreateFlashcardsViaAiRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        _logger.LogInformation("Flashcard generation for document {DocumentId} by user {UserId}", docId, userId);
        var result = await _flashcardAiService.GenerateFlashcardsAsync(docId, request, userId, cancellationToken);

        try
        {
            var document = await _unitOfWork.Documents.GetByIdAsync(docId, cancellationToken);
            if (document is not null)
            {
                await _realTimeNotifier.NotifyNewFlashcardsReadyAsync(userId, docId, document.Title ?? "Document", result.FlashcardLists.Count, cancellationToken);
            }
        }
        catch (Exception notifyEx)
        {
            _logger.LogWarning(notifyEx, "Real-time notify (flashcards ready) failed for document {DocumentId}", docId);
        }

        return Ok(result);
    }

    /// <summary>Generate a quiz from a document using AI.</summary>
    [HttpPost("quizzes/generate")]
    public async Task<ActionResult<QuizResponseDto>>  GenerateQuiz(
        Guid docId,
        [FromBody] CreateQuizRequestViaAiDto dto,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        _logger.LogInformation("Quiz generation for document {DocumentId} by user {UserId}", docId, userId);
        var result = await _quizAiService.GenerateAndPersistQuizAsync(docId, dto, userId, cancellationToken);

        try
        {
            await _realTimeNotifier.NotifyQuizReadyAsync(userId, result.Id, result.Title, cancellationToken);
        }
        catch (Exception notifyEx)
        {
            _logger.LogWarning(notifyEx, "Real-time notify (quiz ready) failed for quiz {QuizId}", result.Id);
        }

        return Ok(result);
    }
}

public record SummarizeRequest(Guid DocumentId);





