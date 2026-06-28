using System.Security.Claims;
using AIStudyHub.Business.DTOs.Common;
using AIStudyHub.Business.DTOs.Gamification;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Data.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class GamificationController : ControllerBase
{
    private readonly IGamificationService _service;

    public GamificationController(IGamificationService service)
    {
        _service = service;
    }

    /// <summary>Returns XP, level, streak info for the current user.</summary>
    [HttpGet("stats")]
    public async Task<ActionResult<UserStatsResponseDto>> MyStats(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();
        return await GetStatsFor(userId, cancellationToken);
    }

    /// <summary>Returns XP, level, streak info for a specific user.</summary>
    [HttpGet("stats/{userId:guid}")]
    public async Task<ActionResult<UserStatsResponseDto>> GetStatsFor(Guid userId, CancellationToken cancellationToken)
    {
        var result = await _service.GetStatsAsync(userId, cancellationToken);
        if (!result.Success) return BadRequest(result.Error);
        return Ok(result.Data);
    }

    /// <summary>Top users by XP. Optional query: ?top=20.</summary>
    [HttpGet("leaderboard")]
    public async Task<ActionResult<IReadOnlyList<LeaderboardEntryDto>>> Leaderboard(
        [FromQuery] int top = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.GetLeaderboardAsync(top, cancellationToken);
        if (!result.Success) return BadRequest(result.Error);
        return Ok(result.Data);
    }

    /// <summary>
    /// Internal endpoint used by other services (or admin tooling) to record an XP award.
    /// In production this would be locked down to server-to-server auth; here it accepts any
    /// authenticated caller because the gamification pipeline runs inside the same API.
    /// </summary>
    [HttpPost("award-xp")]
    public async Task<ActionResult<XpAwardResult>> AwardXp(
        [FromBody] XpAwardRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.AwardXpAsync(request, cancellationToken);
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
