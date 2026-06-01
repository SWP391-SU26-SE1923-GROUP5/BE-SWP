using AIStudyHub.Business.DTOs.Questions;
using FluentValidation;

namespace AIStudyHub.Business.Validators.Questions;

public sealed class CreateQuestionRequestDtoValidator : AbstractValidator<CreateQuestionRequestDto>
{
    public CreateQuestionRequestDtoValidator()
    {
        RuleFor(x => x.QuizId).NotEmpty();
        RuleFor(x => x.Text).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Points).GreaterThan(0);
    }
}

public sealed class UpdateQuestionRequestDtoValidator : AbstractValidator<UpdateQuestionRequestDto>
{
    public UpdateQuestionRequestDtoValidator()
    {
        RuleFor(x => x.Text).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Points).GreaterThan(0);
    }
}
