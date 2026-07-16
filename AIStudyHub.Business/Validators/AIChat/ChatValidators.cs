using AIStudyHub.Business.DTOs.AIChat;
using FluentValidation;

namespace AIStudyHub.Business.Validators.AIChat;

public sealed class CreateChatSessionRequestDtoValidator : AbstractValidator<CreateChatSessionRequestDto>
{
    public CreateChatSessionRequestDtoValidator()
    {
        RuleFor(x => x.SessionTitle).NotEmpty().MaximumLength(64);
    }
}

public sealed class UpdateChatSessionRequestDtoValidator : AbstractValidator<UpdateChatSessionRequestDto>
{
    public UpdateChatSessionRequestDtoValidator()
    {
        RuleFor(x => x.SessionTitle)
            .Must(title => !string.IsNullOrWhiteSpace(title))
            .WithMessage("Session title is required.")
            .Must(title => title?.Trim().Length <= 64)
            .WithMessage("Session title must not exceed 64 characters.");
    }
}

public sealed class CreateChatMessageRequestDtoValidator : AbstractValidator<CreateChatMessageRequestDto>
{
    public CreateChatMessageRequestDtoValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEqual(Guid.Empty)
            .When(x => x.SessionId.HasValue);
        RuleFor(x => x.Message).NotEmpty();
    }
}
