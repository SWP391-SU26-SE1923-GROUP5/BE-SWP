using AIStudyHub.Business.DTOs.Payments;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IPaymentService : ICrudService<PaymentResponseDto, CreatePaymentRequestDto, UpdatePaymentRequestDto>
{
}
