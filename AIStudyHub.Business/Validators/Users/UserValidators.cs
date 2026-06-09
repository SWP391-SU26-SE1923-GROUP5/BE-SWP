using AIStudyHub.Business.DTOs.Users;
using FluentValidation;

namespace AIStudyHub.Business.Validators.Users;

public sealed class CreateUserRequestDtoValidator : AbstractValidator<CreateUserRequestDto>
{
    public CreateUserRequestDtoValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(100);
        RuleFor(x => x.CurrentStorageCapacity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CurrentAiTokenUsage).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Status).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Role).NotEmpty().MaximumLength(20);
    }
}

public sealed class UpdateUserRequestDtoValidator : AbstractValidator<UpdateUserRequestDto>
{
    public UpdateUserRequestDtoValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.CurrentStorageCapacity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CurrentAiTokenUsage).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Status).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Role).NotEmpty().MaximumLength(20);
    }
}
