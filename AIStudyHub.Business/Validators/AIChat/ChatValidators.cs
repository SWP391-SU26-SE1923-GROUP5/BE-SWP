using AIStudyHub.Business.DTOs.AIChat;
using FluentValidation;

namespace AIStudyHub.Business.Validators.AIChat;

public sealed class CreateChatSessionRequestDtoValidator : AbstractValidator<CreateChatSessionRequestDto>
{
    public CreateChatSessionRequestDtoValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.DocumentId).NotEmpty();
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
