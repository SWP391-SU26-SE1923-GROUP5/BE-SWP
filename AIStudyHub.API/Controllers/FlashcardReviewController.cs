using System.Security.Claims;
using AIStudyHub.Business.DTOs.Common;
using AIStudyHub.Business.DTOs.FlashcardReviews;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Data.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

/// <summary>
/// Spaced Repetition System endpoints. Implements the SM-2 algorithm from message.txt section 3.5.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class FlashcardReviewController : ControllerBase
{
    private readonly IFlashcardReviewService _service;

    public FlashcardReviewController(IFlashcardReviewService service)
    {
        _service = service;
    }

    /// <summary>Submit a review for a flashcard. Triggers SM-2 update and returns the new schedule
    /// + any badges the user just unlocked (Plan C3).</summary>
    [HttpPost("review")]
    public async Task<ActionResult<ReviewFlashcardResultDto>> SubmitReview(
        [FromBody] ReviewFlashcardRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await _service.ProcessReviewAsync(
            userId,
            request.FlashcardId,
            request.Quality,
            request.TimeSpentSeconds,
            cancellationToken);
        if (!result.Success) return BadRequest(result.Error);
        return Ok(result.Data);
    }

    /// <summary>Get flashcards due for review now (for the dashboard "Due Today" badge).</summary>
    [HttpGet("due")]
    public async Task<ActionResult<IReadOnlyList<DueFlashcardDto>>> GetDue(
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await _service.GetDueAsync(userId, limit, cancellationToken);
        if (!result.Success) return BadRequest(result.Error);
        return Ok(result.Data);
    }

    /// <summary>Get the count of flashcards currently due. Lightweight call for badge counters.</summary>
    [HttpGet("due/count")]
    public async Task<ActionResult<int>> GetDueCount(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();
        return Ok(await _service.CountDueAsync(userId, cancellationToken));
    }

    [HttpGet("history")]
    public async Task<ActionResult<PagedResultDto<FlashcardReviewHistoryItemDto>>> GetHistory(
        [FromQuery] Guid? documentId,
        [FromQuery] Guid? flashcardId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var pagination = new PaginationParams
        {
            Offset = Math.Max(0, offset),
            Limit = limit
        };
        var result = await _service.GetHistoryAsync(
            userId,
            documentId,
            flashcardId,
            fromDate,
            toDate,
            pagination,
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("history/{attemptId:guid}")]
    public async Task<ActionResult<FlashcardReviewHistoryDetailDto>> GetHistoryDetail(
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await _service.GetHistoryDetailAsync(
            userId,
            attemptId,
            cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Get review statistics for the current user.</summary>
    [HttpGet("stats")]
    public async Task<ActionResult<FlashcardReviewStatsDto>> GetStats(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await _service.GetStatsAsync(userId, cancellationToken);
        if (!result.Success) return BadRequest(result.Error);
        return Ok(result.Data);
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
