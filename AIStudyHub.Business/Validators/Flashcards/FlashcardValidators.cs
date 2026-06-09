using AIStudyHub.Business.DTOs.Flashcards;
using FluentValidation;

namespace AIStudyHub.Business.Validators.Flashcards;

public sealed class CreateFlashcardRequestDtoValidator : AbstractValidator<CreateFlashcardRequestDto>
{
    public CreateFlashcardRequestDtoValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.Front).NotEmpty();
        RuleFor(x => x.Back).NotEmpty();
    }
}

public sealed class UpdateFlashcardRequestDtoValidator : AbstractValidator<UpdateFlashcardRequestDto>
{
    public UpdateFlashcardRequestDtoValidator()
    {
        RuleFor(x => x.Front).NotEmpty();
        RuleFor(x => x.Back).NotEmpty();
    }
}
