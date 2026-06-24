using AIStudyHub.Business.Interfaces.AI.Orchestration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class RagController : ControllerBase
{
    private readonly ISemanticKernelOrchestrator _orchestrator;
    private readonly ILogger<RagController> _logger;

    public RagController(
        ISemanticKernelOrchestrator orchestrator,
        ILogger<RagController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] AskRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest("Question is required");
        }

        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        try
        {
            _logger.LogInformation("RAG query from user {UserId}: {Question}", userId, request.Question);
            
            var response = await _orchestrator.AskAsync(userId, null, request.Question, new List<AIStudyHub.Data.Entities.ChatMessage>(), ct);

            return Ok(new
            {
                answer = response.Answer,
                citations = response.Citations,
                confidence = response.Confidence
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing RAG query for user {UserId}", userId);
            return StatusCode(500, "An error occurred while processing your question");
        }
    }

    [HttpPost("summarize")]
    public async Task<IActionResult> Summarize([FromBody] SummarizeRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
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

    private Guid GetUserId()
    {
        var claim = User.FindFirst("sub")?.Value 
                  ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        
        return Guid.TryParse(claim, out var userId) ? userId : Guid.Empty;
    }
}

public record AskRequest(string Question);
public record SummarizeRequest(Guid DocumentId);
