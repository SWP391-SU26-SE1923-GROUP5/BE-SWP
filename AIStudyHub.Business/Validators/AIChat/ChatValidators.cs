using AIStudyHub.Business.DTOs.AIChat;
using FluentValidation;

namespace AIStudyHub.Business.Validators.AIChat;

public sealed class CreateChatSessionRequestDtoValidator : AbstractValidator<CreateChatSessionRequestDto>
{
    public CreateChatSessionRequestDtoValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateChatMessageRequestDtoValidator : AbstractValidator<CreateChatMessageRequestDto>
{
    public CreateChatMessageRequestDtoValidator()
    {
        RuleFor(x => x.ChatSessionId).NotEmpty();
        RuleFor(x => x.Role).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Content).NotEmpty().MaximumLength(8000);
    }
}
