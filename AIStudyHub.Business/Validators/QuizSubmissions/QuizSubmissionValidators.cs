using System.Text.Json;
using AIStudyHub.Business.DTOs.QuizSubmissions;
using FluentValidation;

namespace AIStudyHub.Business.Validators.QuizSubmissions;

public sealed class CreateQuizSubmissionRequestDtoValidator : AbstractValidator<CreateQuizSubmissionRequestDto>
{
    public CreateQuizSubmissionRequestDtoValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.QuizId).NotEmpty();
        RuleFor(x => x.Answers)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(BeValidAnswers)
            .WithMessage(
                "Answers must be a JSON object whose keys are non-empty question GUIDs and whose values are non-blank selections.");
        RuleFor(x => x.DurationSeconds)
            .InclusiveBetween(1, 86_400)
            .When(x => x.DurationSeconds.HasValue);
    }

    private static bool BeValidAnswers(string answers)
    {
        if (string.IsNullOrWhiteSpace(answers))
            return false;

        try
        {
            var selections = JsonSerializer.Deserialize<Dictionary<string, string>>(answers);
            return selections is not null
                && selections.All(selection =>
                    Guid.TryParse(selection.Key, out var questionId)
                    && questionId != Guid.Empty
                    && !string.IsNullOrWhiteSpace(selection.Value));
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
