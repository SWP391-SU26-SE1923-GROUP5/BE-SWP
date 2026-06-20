using AIStudyHub.Business.DTOs.AIChat;
using FluentValidation;

namespace AIStudyHub.Business.Validators.AIChat;

public sealed class CreateChatSessionRequestDtoValidator : AbstractValidator<CreateChatSessionRequestDto>
{
    public CreateChatSessionRequestDtoValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.DocumentId)
            .NotEqual(Guid.Empty)
            .When(x => x.DocumentId.HasValue)
            .WithMessage("DocumentId, if provided, must not be empty.");
        RuleFor(x => x.SessionTitle).NotEmpty().MaximumLength(64);
    }
}

public sealed class CreateChatMessageRequestDtoValidator : AbstractValidator<CreateChatMessageRequestDto>
{
    public CreateChatMessageRequestDtoValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.Message).NotEmpty();
    }
}
