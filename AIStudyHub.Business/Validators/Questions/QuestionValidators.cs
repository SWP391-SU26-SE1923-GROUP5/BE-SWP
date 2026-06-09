using AIStudyHub.Business.DTOs.Questions;
using FluentValidation;

namespace AIStudyHub.Business.Validators.Questions;

public sealed class CreateQuestionRequestDtoValidator : AbstractValidator<CreateQuestionRequestDto>
{
    public CreateQuestionRequestDtoValidator()
    {
        RuleFor(x => x.QuizId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty();
    }
}

public sealed class UpdateQuestionRequestDtoValidator : AbstractValidator<UpdateQuestionRequestDto>
{
    public UpdateQuestionRequestDtoValidator()
    {
        RuleFor(x => x.Title).NotEmpty();
    }
}
