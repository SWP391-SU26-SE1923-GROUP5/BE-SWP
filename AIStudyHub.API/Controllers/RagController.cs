using AIStudyHub.Business.Services;
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
            
            var response = await _orchestrator.AskAsync(userId, request.Question, ct);

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

    private Guid GetUserId()
    {
        var claim = User.FindFirst("sub")?.Value 
                 ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        
        return Guid.TryParse(claim, out var userId) ? userId : Guid.Empty;
    }
}

public record AskRequest(string Question);
