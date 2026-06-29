using System.Security.Claims;
using AIStudyHub.Business.DTOs.Subscriptions;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

/// <summary>
/// Plan C4 / B.4.4 — read-only subscription summary for the current user.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionService _service;

    public SubscriptionsController(ISubscriptionService service)
    {
        _service = service;
    }

    [HttpGet("me")]
    public async Task<ActionResult<MySubscriptionDto>> GetMy(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await _service.GetMySubscriptionAsync(userId, cancellationToken);
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