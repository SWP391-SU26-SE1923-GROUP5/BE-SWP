using AIStudyHub.Data.Enums;

namespace AIStudyHub.Business.DTOs.Payments;

public sealed record PaymentResponseDto(Guid Id, Guid UserId, decimal Amount, string Currency, string Provider, string ProviderTransactionId, PaymentStatus Status, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record CreatePaymentRequestDto(Guid UserId, decimal Amount, string Currency, string Provider);

public sealed record UpdatePaymentRequestDto(string ProviderTransactionId, PaymentStatus Status);
