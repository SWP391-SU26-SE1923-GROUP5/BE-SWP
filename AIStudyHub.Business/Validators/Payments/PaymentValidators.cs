using AIStudyHub.Business.DTOs.Payments;
using FluentValidation;

namespace AIStudyHub.Business.Validators.Payments;

public sealed class CreatePaymentRequestDtoValidator : AbstractValidator<CreatePaymentRequestDto>
{
    public CreatePaymentRequestDtoValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.Provider).NotEmpty().MaximumLength(100);
    }
}

public sealed class UpdatePaymentRequestDtoValidator : AbstractValidator<UpdatePaymentRequestDto>
{
    public UpdatePaymentRequestDtoValidator()
    {
        RuleFor(x => x.ProviderTransactionId).MaximumLength(200);
        RuleFor(x => x.Status).IsInEnum();
    }
}
