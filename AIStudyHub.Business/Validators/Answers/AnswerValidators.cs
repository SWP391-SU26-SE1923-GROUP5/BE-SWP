using AIStudyHub.Business.DTOs.Answers;
using FluentValidation;

namespace AIStudyHub.Business.Validators.Answers;

public sealed class CreateAnswerRequestDtoValidator : AbstractValidator<CreateAnswerRequestDto>
{
    public CreateAnswerRequestDtoValidator()
    {
        RuleFor(x => x.QuestionId).NotEmpty();
        RuleFor(x => x.SelectedOption).NotEmpty();
    }
}

public sealed class UpdateAnswerRequestDtoValidator : AbstractValidator<UpdateAnswerRequestDto>
{
    public UpdateAnswerRequestDtoValidator()
    {
        RuleFor(x => x.SelectedOption).NotEmpty();
    }
}
