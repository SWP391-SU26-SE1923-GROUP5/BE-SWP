using AIStudyHub.Business.DTOs.Notifications;
using FluentValidation;

namespace AIStudyHub.Business.Validators.Notifications;

public sealed class CreateNotificationRequestDtoValidator : AbstractValidator<CreateNotificationRequestDto>
{
    public CreateNotificationRequestDtoValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Message).NotEmpty();
    }
}

public sealed class UpdateNotificationRequestDtoValidator : AbstractValidator<UpdateNotificationRequestDto>
{
    public UpdateNotificationRequestDtoValidator()
    {
        RuleFor(x => x.Message).NotEmpty();
    }
}
