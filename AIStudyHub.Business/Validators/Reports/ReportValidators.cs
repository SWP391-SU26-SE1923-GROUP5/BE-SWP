using AIStudyHub.Business.DTOs.Reports;
using FluentValidation;

namespace AIStudyHub.Business.Validators.Reports;

public sealed class CreateReportRequestDtoValidator : AbstractValidator<CreateReportRequestDto>
{
    public CreateReportRequestDtoValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.DocumentId).NotEmpty();
    }
}

public sealed class UpdateReportRequestDtoValidator : AbstractValidator<UpdateReportRequestDto>
{
    public UpdateReportRequestDtoValidator()
    {
    }
}
