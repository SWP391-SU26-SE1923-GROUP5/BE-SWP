using AIStudyHub.Business.DTOs.Payments;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class PaymentController : ControllerBase
{
    private readonly IPaymentService _service;

    public PaymentController(IPaymentService service)
    {
        _service = service;
    }

    /// <summary>Lấy tất cả giao dịch thanh toán (Admin only).</summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<PaymentResponseDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>Lấy thông tin giao dịch theo ID (Admin only).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PaymentResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Lấy lịch sử giao dịch của user hiện tại.</summary>
    [HttpGet("my")]
    public async Task<ActionResult<IReadOnlyList<PaymentResponseDto>>> GetMyPayments(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await _service.GetUserPaymentsAsync(userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>Admin: Hoàn tiền một giao dịch.</summary>
    [HttpPost("{id:guid}/refund")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RefundPayment(Guid id, CancellationToken cancellationToken)
    {
        await _service.RefundPaymentAsync(id, cancellationToken);
        return NoContent();
    }

    // POST   /api/Payment  - Đã xóa. Giao dịch thanh toán phải đi qua cổng thanh toán (Webhook).
    // PUT    /api/Payment/{id} - Đã xóa. Cập nhật giao dịch phải đi qua cổng thanh toán.
    // DELETE /api/Payment/{id} - Đã xóa. Không cho phép xóa giao dịch.

    [HttpPost("create-checkout-url")]
    public async Task<ActionResult<PaymentLinkResponseDto>> CreatePaymentUrl([FromBody] CreatePaymentLinkRequestDto request, CancellationToken cancellationToken)
    {
        var response = await _service.CreatePaymentUrlAsync(request, HttpContext, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Frontend fetches this after VNPay redirects to it.
    /// Returns JSON instead of Redirect to avoid CORS.
    /// </summary>
    [HttpGet("vnpay-return")]
    [AllowAnonymous]
    public async Task<IActionResult> PaymentReturn(CancellationToken cancellationToken)
    {
        var result = await _service.HandleVnpayReturnAsync(Request.Query, cancellationToken);
        if (!result.IsValidSignature)
        {
            return BadRequest(new { success = false, message = "Invalid signature" });
        }

        return Ok(new
        {
            success = result.IsSuccess,
            message = result.Message,
            status = result.Status
        });
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
}
