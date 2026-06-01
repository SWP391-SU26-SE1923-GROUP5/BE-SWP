using AIStudyHub.Business.DTOs.Reports;
using FluentValidation;

namespace AIStudyHub.Business.Validators.Reports;

public sealed class CreateReportRequestDtoValidator : AbstractValidator<CreateReportRequestDto>
{
    public CreateReportRequestDtoValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Details).MaximumLength(2000);
    }
}

public sealed class UpdateReportRequestDtoValidator : AbstractValidator<UpdateReportRequestDto>
{
    public UpdateReportRequestDtoValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
    }
}
