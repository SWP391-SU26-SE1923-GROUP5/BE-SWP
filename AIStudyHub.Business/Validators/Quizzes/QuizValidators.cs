using AIStudyHub.Business.DTOs.Quizzes;
using FluentValidation;

namespace AIStudyHub.Business.Validators.Quizzes;

public sealed class CreateQuizRequestDtoValidator : AbstractValidator<CreateQuizRequestDto>
{
    public CreateQuizRequestDtoValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.TimeLimitMinutes).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PassingScore).InclusiveBetween(0, 100);
    }
}

public sealed class UpdateQuizRequestDtoValidator : AbstractValidator<UpdateQuizRequestDto>
{
    public UpdateQuizRequestDtoValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.TimeLimitMinutes).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PassingScore).InclusiveBetween(0, 100);
    }
}
