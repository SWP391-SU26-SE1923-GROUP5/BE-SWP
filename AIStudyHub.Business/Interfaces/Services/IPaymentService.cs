using AIStudyHub.Business.DTOs.Payments;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IPaymentService : ICrudService<PaymentResponseDto, CreatePaymentRequestDto, UpdatePaymentRequestDto>
{
    System.Threading.Tasks.Task<PaymentLinkResponseDto> CreatePaymentUrlAsync(CreatePaymentLinkRequestDto request, Microsoft.AspNetCore.Http.HttpContext context, System.Threading.CancellationToken cancellationToken = default);
    System.Threading.Tasks.Task<bool> ProcessVnPayWebhookAsync(Microsoft.AspNetCore.Http.IQueryCollection query, System.Threading.CancellationToken cancellationToken = default);
    System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<PaymentResponseDto>> GetUserPaymentsAsync(Guid userId, System.Threading.CancellationToken cancellationToken = default);
    System.Threading.Tasks.Task RefundPaymentAsync(Guid paymentId, System.Threading.CancellationToken cancellationToken = default);
}
