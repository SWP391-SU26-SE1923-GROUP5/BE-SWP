using AIStudyHub.Business.DTOs.Payments;
using Microsoft.AspNetCore.Http;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IPaymentService
{
    Task<IReadOnlyList<PaymentResponseDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PaymentResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PaymentLinkResponseDto> CreatePaymentUrlAsync(CreatePaymentLinkRequestDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentResponseDto>> GetUserPaymentsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task RefundPaymentAsync(Guid paymentId, CancellationToken cancellationToken = default);
    Task<VnpayReturnResult> HandleVnpayReturnAsync(IQueryCollection query, CancellationToken cancellationToken = default);
}
