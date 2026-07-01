using System.Security.Claims;
using AIStudyHub.Business.DTOs.Common;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

/// <summary>
/// Plan A.1 / B.4.1 — Aggregated learning dashboard endpoint.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _service;

    public AnalyticsController(IAnalyticsService service)
    {
        _service = service;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardDto>> GetDashboard(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await _service.GetDashboardAsync(userId, cancellationToken);
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
