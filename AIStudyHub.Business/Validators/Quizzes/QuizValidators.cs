using AIStudyHub.Business.DTOs.Quizzes;
using FluentValidation;

namespace AIStudyHub.Business.Validators.Quizzes;

public sealed class CreateQuizRequestDtoValidator : AbstractValidator<CreateQuizRequestDto>
{
    public CreateQuizRequestDtoValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(255);
    }
}

public sealed class UpdateQuizRequestDtoValidator : AbstractValidator<UpdateQuizRequestDto>
{
    public UpdateQuizRequestDtoValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(255);
    }
}
