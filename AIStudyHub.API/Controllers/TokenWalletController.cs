using AIStudyHub.Business.DTOs.TokenWallet;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class TokenWalletController : ControllerBase
{
    private readonly ITokenWalletService _service;

    public TokenWalletController(ITokenWalletService service) => _service = service;

    [HttpGet("balance")]
    public async Task<ActionResult<TokenWalletResponseDto>> GetBalance(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();
        var result = await _service.GetBalanceAsync(userId, ct);
        return Ok(result);
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub")
            ?? User.FindFirst("userId");
        return claim != null && Guid.TryParse(claim.Value, out var id) ? id : Guid.Empty;
    }
}
