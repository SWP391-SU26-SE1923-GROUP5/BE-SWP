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

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var minDate = today.AddYears(-120);
        var maxDate = today.AddYears(-18);

        RuleFor(x => x.DateOfBirth)
            .NotEmpty().WithMessage("Date of birth is required.")
            .GreaterThanOrEqualTo(minDate).WithMessage("User cannot be older than 120 years.")
            .LessThanOrEqualTo(maxDate).WithMessage("User must be at least 18 years old.");
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

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var minDate = today.AddYears(-120);
        var maxDate = today.AddYears(-18);

        RuleFor(x => x.DateOfBirth)
            .NotEmpty().WithMessage("Date of birth is required.")
            .GreaterThanOrEqualTo(minDate).WithMessage("User cannot be older than 120 years.")
            .LessThanOrEqualTo(maxDate).WithMessage("User must be at least 18 years old.");
    }
}

public sealed class UpdateProfileRequestDtoValidator : AbstractValidator<UpdateProfileRequestDto>
{
    public UpdateProfileRequestDtoValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(255);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var minDate = today.AddYears(-120);
        var maxDate = today.AddYears(-18);

        RuleFor(x => x.DateOfBirth)
            .NotEmpty().WithMessage("Date of birth is required.")
            .GreaterThanOrEqualTo(minDate).WithMessage("User cannot be older than 120 years.")
            .LessThanOrEqualTo(maxDate).WithMessage("User must be at least 18 years old.");
    }
}
