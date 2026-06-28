using AIStudyHub.Business.DTOs.Reports;
using FluentValidation;

namespace AIStudyHub.Business.Validators.Reports;

public sealed class CreateReportRequestDtoValidator : AbstractValidator<CreateReportRequestDto>
{
    public CreateReportRequestDtoValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Reason)
            .NotEmpty().When(x => x.Category == ReportCategoryDto.Other)
            .Length(10, 500).When(x => x.Category == ReportCategoryDto.Other);
    }
}

public sealed class UpdateReportRequestDtoValidator : AbstractValidator<UpdateReportRequestDto>
{
    public UpdateReportRequestDtoValidator()
    {
    }
}

public sealed class UpdateReportStatusRequestDtoValidator : AbstractValidator<UpdateReportStatusRequestDto>
{
    public UpdateReportStatusRequestDtoValidator()
    {
        RuleFor(x => x.Status).IsInEnum().NotEqual(ReportStatusDto.Pending);
    }
}

public sealed class ReportFilterDtoValidator : AbstractValidator<ReportFilterDto>
{
    public ReportFilterDtoValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
