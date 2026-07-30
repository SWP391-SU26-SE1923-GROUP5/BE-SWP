using AIStudyHub.Business.DTOs.FlashcardReviews;
using FluentValidation;

namespace AIStudyHub.Business.Validators.FlashcardReviews;

public sealed class ReviewFlashcardRequestDtoValidator : AbstractValidator<ReviewFlashcardRequestDto>
{
    public ReviewFlashcardRequestDtoValidator()
    {
        RuleFor(x => x.FlashcardId).NotEmpty();
        RuleFor(x => x.Quality).IsInEnum();
        RuleFor(x => x.TimeSpentSeconds)
            .InclusiveBetween(1, 86_400)
            .When(x => x.TimeSpentSeconds.HasValue);
    }
}
