using System.Security.Claims;
using AIStudyHub.Business.DTOs.Recommendations;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class RecommendationsController : ControllerBase
{
    private readonly IRecommendationService _service;

    public RecommendationsController(IRecommendationService service)
    {
        _service = service;
    }

    [HttpGet("mastery")]
    public async Task<ActionResult<IReadOnlyList<SubjectMasteryDto>>> MyMastery(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();
        return await GetMasteryFor(userId, cancellationToken);
    }

    [HttpGet("mastery/{userId:guid}")]
    public async Task<ActionResult<IReadOnlyList<SubjectMasteryDto>>> GetMasteryFor(Guid userId, CancellationToken cancellationToken)
    {
        var result = await _service.GetSubjectMasteryAsync(userId, cancellationToken);
        if (!result.Success) return BadRequest(result.Error);
        return Ok(result.Data);
    }

    [HttpGet("suggestions")]
    public async Task<ActionResult<RecommendationResultDto>> MySuggestions(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();
        return await GetSuggestionsFor(userId, cancellationToken);
    }

    [HttpGet("suggestions/{userId:guid}")]
    public async Task<ActionResult<RecommendationResultDto>> GetSuggestionsFor(Guid userId, CancellationToken cancellationToken)
    {
        var result = await _service.GetRecommendationsAsync(userId, cancellationToken);
        if (!result.Success) return BadRequest(result.Error);
        return Ok(result.Data);
    }

    /// <summary>Returns the user's active (non-dismissed) recommendations with an ETag for cache invalidation.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RecommendationResponseDto>>> GetMyActive(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();
        var result = await _service.GetMyActiveRecommendationsAsync(userId, ct);

        // ETag based on count: changes only when recommendations are added/dismissed
        var etag = $"\"{result.Count}:{result.GetHashCode()}\"";
        Response.Headers["ETag"] = etag;
        return Ok(result);
    }

    /// <summary>Dismisses a recommendation (sets status to Dismissed).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Dismiss(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();
        await _service.DismissAsync(id, userId, ct);
        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub")
            ?? User.FindFirst("userId");

        return claim != null && Guid.TryParse(claim.Value, out var userId)
            ? userId
            : Guid.Empty;
    }
}
