using AIStudyHub.Business.DTOs.Subjects;
using FluentValidation;

namespace AIStudyHub.Business.Validators.Subjects;

public sealed class CreateSubjectRequestValidator : AbstractValidator<CreateSubjectRequestDto>
{
    public CreateSubjectRequestValidator()
    {
        RuleFor(x => x.SubjectCode)
            .NotEmpty().WithMessage("Subject code is required.")
            .MaximumLength(50).WithMessage("Subject code must not exceed 50 characters.");

        RuleFor(x => x.SubjectName)
            .NotEmpty().WithMessage("Subject name is required.")
            .MaximumLength(255).WithMessage("Subject name must not exceed 255 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters.");
    }
}

public sealed class UpdateSubjectRequestValidator : AbstractValidator<UpdateSubjectRequestDto>
{
    public UpdateSubjectRequestValidator()
    {
        RuleFor(x => x.SubjectCode)
            .NotEmpty().WithMessage("Subject code is required.")
            .MaximumLength(50).WithMessage("Subject code must not exceed 50 characters.");

        RuleFor(x => x.SubjectName)
            .NotEmpty().WithMessage("Subject name is required.")
            .MaximumLength(255).WithMessage("Subject name must not exceed 255 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters.");
    }
}
