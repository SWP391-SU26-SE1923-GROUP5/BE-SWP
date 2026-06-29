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
    private readonly IBadgeService _badgeService;

    public GamificationController(IGamificationService service, IBadgeService badgeService)
    {
        _service = service;
        _badgeService = badgeService;
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

    /// <summary>
    /// Top users by XP for a time window. Query params: <c>?top=20&amp;period=weekly|monthly|alltime</c>.
    /// Defaults to <c>alltime</c> when <c>period</c> is omitted or unknown (backward-compatible).
    /// </summary>
    [HttpGet("leaderboard")]
    public async Task<ActionResult<IReadOnlyList<LeaderboardEntryDto>>> Leaderboard(
        [FromQuery] int top = 20,
        [FromQuery] string period = "alltime",
        CancellationToken cancellationToken = default)
    {
        if (!TryParsePeriod(period, out var parsed))
        {
            return BadRequest("period must be one of: weekly, monthly, alltime.");
        }

        var result = await _service.GetLeaderboardAsync(top, parsed, cancellationToken);
        if (!result.Success) return BadRequest(result.Error);
        return Ok(result.Data);
    }

    private static bool TryParsePeriod(string? raw, out LeaderboardPeriod parsed)
    {
        switch (raw?.Trim().ToLowerInvariant())
        {
            case "weekly":
                parsed = LeaderboardPeriod.Weekly;
                return true;
            case "monthly":
                parsed = LeaderboardPeriod.Monthly;
                return true;
            case "alltime":
            case "all-time":
            case "":
            case null:
                parsed = LeaderboardPeriod.AllTime;
                return true;
            default:
                parsed = LeaderboardPeriod.AllTime;
                return false;
        }
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

    /// <summary>
    /// Plan A.6 — full catalogue of all badges + the caller's progress / unlock state.
    /// Frontend renders a trophy-case plus a progress bar per badge.
    /// </summary>
    [HttpGet("achievements")]
    public async Task<ActionResult<IReadOnlyList<AchievementDto>>> GetAchievements(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();
        var result = await _badgeService.GetAchievementsAsync(userId, cancellationToken);
        return Ok(result);
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
