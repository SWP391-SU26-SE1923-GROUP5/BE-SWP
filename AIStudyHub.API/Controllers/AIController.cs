using AIStudyHub.Business.DTOs.Flashcards;
using AIStudyHub.Business.DTOs.Quizzes;
using AIStudyHub.Business.Interfaces.AI.Generators;
using AIStudyHub.Business.Interfaces.AI.Orchestration;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/AI")]
public sealed class AIController : ControllerBase
{
    private readonly ISemanticKernelOrchestrator _orchestrator;
    private readonly IFlashcardAiService _flashcardAiService;
    private readonly IQuizAiService _quizAiService;
    private readonly ILogger<AIController> _logger;
    private readonly IRealTimeNotificationService _realTimeNotifier;
    private readonly AIStudyHub.Data.Interfaces.IUnitOfWork _unitOfWork;

    public AIController(
        ISemanticKernelOrchestrator orchestrator,
        IFlashcardAiService flashcardAiService,
        IQuizAiService quizAiService,
        ILogger<AIController> logger,
        IRealTimeNotificationService realTimeNotifier,
        AIStudyHub.Data.Interfaces.IUnitOfWork unitOfWork)
    {
        _orchestrator = orchestrator;
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

    ///// <summary>Ask a question about the user's documents using RAG.</summary>
    //[HttpPost("rag/ask")]
    //public async Task<IActionResult> Ask([FromBody] AskRequest request, CancellationToken ct)
    //{
    //    if (string.IsNullOrWhiteSpace(request.Question))
    //        return BadRequest("Question is required");

    //    var userId = GetCurrentUserId();
    //    if (userId == Guid.Empty)
    //        return Unauthorized();

    //    try
    //    {
    //        _logger.LogInformation("RAG query from user {UserId}: {Question}", userId, request.Question);
    //        var response = await _orchestrator.AskAsync(userId, null, request.Question, [], ct);

    //        return Ok(new
    //        {
    //            answer = response.Answer,
    //            citations = response.Citations,
    //            confidence = response.Confidence
    //        });
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "Error processing RAG query for user {UserId}", userId);
    //        return StatusCode(500, "An error occurred while processing your question");
    //    }
    //}

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
    public async Task<ActionResult<IReadOnlyList<FlashcardResponseDto>>> GenerateFlashcards(
        Guid docId,
        [FromBody] CreateFlashcardsViaAiRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        try
        {
            _logger.LogInformation("Flashcard generation for document {DocumentId} by user {UserId}", docId, userId);
            var result = await _flashcardAiService.GenerateFlashcardsAsync(docId, request, userId, cancellationToken);

            try
            {
                var document = await _unitOfWork.Documents.GetByIdAsync(docId, cancellationToken);
                if (document is not null)
                {
                    await _realTimeNotifier.NotifyNewFlashcardsReadyAsync(userId, docId, document.Title ?? "Document", result.Count, cancellationToken);
                }
            }
            catch (Exception notifyEx)
            {
                _logger.LogWarning(notifyEx, "Real-time notify (flashcards ready) failed for document {DocumentId}", docId);
            }

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating flashcards for document {DocumentId}", docId);
            return StatusCode(500, "An error occurred while generating flashcards");
        }
    }

    /// <summary>Generate a quiz from a document using AI.</summary>
    [HttpPost("quizzes/generate")]
    public async Task<ActionResult<QuizResponseDto>>  GenerateQuiz(
        Guid docId,
        [FromBody] CreateQuizRequestViaAIDto dto,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        if (dto.numberOfQuestions <= 0 || dto.numberOfQuestions > 20)
            return BadRequest("Number of questions must be between 1 and 20.");

        try
        {
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
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating quiz for document {DocumentId}", docId);
            return StatusCode(500, "An error occurred while generating the quiz");
        }
    }
}

public record AskRequest(string Question);
public record SummarizeRequest(Guid DocumentId);





