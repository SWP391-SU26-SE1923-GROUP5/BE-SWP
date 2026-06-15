using AIStudyHub.Data.Enums;

namespace AIStudyHub.Business.DTOs.Payments;

public sealed record PaymentResponseDto(Guid Id, Guid UserId, string PaymentInfo, DateTime PaymentDate, PaymentStatus? Status, Guid? TierId, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record CreatePaymentRequestDto(Guid UserId, string PaymentInfo, DateTime? PaymentDate, Guid? TierId);

public sealed record UpdatePaymentRequestDto(string PaymentInfo, PaymentStatus? Status, Guid? TierId);

public sealed record CreatePaymentLinkRequestDto(Guid TierId);

public sealed record PaymentLinkResponseDto(string PaymentUrl);
