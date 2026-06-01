using AIStudyHub.Business.DTOs.Payments;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[Route("api/[controller]")]
public sealed class PaymentController : CrudControllerBase<PaymentResponseDto, CreatePaymentRequestDto, UpdatePaymentRequestDto>
{
    public PaymentController(IPaymentService service)
        : base(service)
    {
    }
}
