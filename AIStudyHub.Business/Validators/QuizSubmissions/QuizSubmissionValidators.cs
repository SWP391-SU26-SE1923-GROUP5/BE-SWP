using AIStudyHub.Business.DTOs.QuizSubmissions;
using FluentValidation;

namespace AIStudyHub.Business.Validators.QuizSubmissions;

public sealed class CreateQuizSubmissionRequestDtoValidator : AbstractValidator<CreateQuizSubmissionRequestDto>
{
    public CreateQuizSubmissionRequestDtoValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.QuizId).NotEmpty();
        RuleFor(x => x.Answers).NotEmpty();
    }
}
