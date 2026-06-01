using AIStudyHub.Business.DTOs.Notifications;
using FluentValidation;

namespace AIStudyHub.Business.Validators.Notifications;

public sealed class CreateNotificationRequestDtoValidator : AbstractValidator<CreateNotificationRequestDto>
{
    public CreateNotificationRequestDtoValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Message).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Type).IsInEnum();
    }
}

public sealed class UpdateNotificationRequestDtoValidator : AbstractValidator<UpdateNotificationRequestDto>
{
    public UpdateNotificationRequestDtoValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Message).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Type).IsInEnum();
    }
}
