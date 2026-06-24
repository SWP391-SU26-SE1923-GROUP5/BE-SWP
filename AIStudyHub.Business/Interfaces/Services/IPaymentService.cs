using AIStudyHub.Business.DTOs.Payments;
using Microsoft.AspNetCore.Http;
using System;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IPaymentService : ICrudService<PaymentResponseDto, CreatePaymentRequestDto, UpdatePaymentRequestDto>
{
    Task<PaymentLinkResponseDto> CreatePaymentUrlAsync(CreatePaymentLinkRequestDto request, HttpContext context, CancellationToken cancellationToken = default);
    Task<System.Collections.Generic.IReadOnlyList<PaymentResponseDto>> GetUserPaymentsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task RefundPaymentAsync(Guid paymentId, CancellationToken cancellationToken = default);
    Task<VnpayReturnResult> HandleVnpayReturnAsync(IQueryCollection query, CancellationToken cancellationToken = default);
}
