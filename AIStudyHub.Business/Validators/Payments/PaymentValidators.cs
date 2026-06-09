using AIStudyHub.Business.DTOs.Payments;
using FluentValidation;

namespace AIStudyHub.Business.Validators.Payments;

public sealed class CreatePaymentRequestDtoValidator : AbstractValidator<CreatePaymentRequestDto>
{
    public CreatePaymentRequestDtoValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.PaymentInfo).NotEmpty();
    }
}

public sealed class UpdatePaymentRequestDtoValidator : AbstractValidator<UpdatePaymentRequestDto>
{
    public UpdatePaymentRequestDtoValidator()
    {
        RuleFor(x => x.PaymentInfo).NotEmpty();
    }
}
