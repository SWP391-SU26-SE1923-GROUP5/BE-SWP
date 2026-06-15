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

    // POST   /api/Payment  - Đã xóa. Giao dịch thanh toán phải đi qua cổng thanh toán (Webhook).
    // PUT    /api/Payment/{id} - Đã xóa. Cập nhật giao dịch phải đi qua cổng thanh toán.
    // DELETE /api/Payment/{id} - Đã xóa. Không cho phép xóa giao dịch.

    [HttpPost("create-checkout-url")]
    public async Task<ActionResult<PaymentLinkResponseDto>> CreatePaymentUrl([FromBody] CreatePaymentLinkRequestDto request, CancellationToken cancellationToken)
    {
        var response = await _service.CreatePaymentUrlAsync(request, HttpContext, cancellationToken);
        return Ok(response);
    }

    [HttpGet("vnpay-return")]
    [AllowAnonymous]
    public IActionResult PaymentReturn()
    {
        // Giao diện web hiển thị kết quả cho user sau khi thanh toán trên cổng VNPay
        var responseCode = Request.Query["vnp_ResponseCode"];
        if (responseCode == "00")
        {
            return Ok("Thanh toán thành công. Cảm ơn bạn!");
        }
        return BadRequest("Thanh toán thất bại hoặc đã bị hủy.");
    }

    [HttpGet("vnpay-ipn")]
    [AllowAnonymous]
    public async Task<IActionResult> PaymentIpn(CancellationToken cancellationToken)
    {
        // VNPay gọi ngầm API này để cập nhật trạng thái đơn hàng
        var success = await _service.ProcessVnPayWebhookAsync(Request.Query, cancellationToken);
        if (success)
        {
            return Ok(new { RspCode = "00", Message = "Confirm Success" });
        }
        
        return Ok(new { RspCode = "97", Message = "Invalid Signature or Payment failed" });
    }
}
