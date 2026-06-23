using AIStudyHub.Business.DTOs.Authentication;
using FluentValidation;
using System.Net.Mail;

namespace AIStudyHub.Business.Validators.Authentication;

public sealed class RegisterRequestDtoValidator : AbstractValidator<RegisterRequestDto>
{
    public RegisterRequestDtoValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(255);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var minDate = today.AddYears(-120);
        var maxDate = today.AddYears(-18);

        RuleFor(x => x.DateOfBirth)
            .NotEmpty().WithMessage("Date of birth is required.")
            .GreaterThanOrEqualTo(minDate).WithMessage("User cannot be older than 120 years.")
            .LessThanOrEqualTo(maxDate).WithMessage("User must be at least 18 years old.");

        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(255)
            .Must(BeValidEmailAddress)
            .WithMessage("Email must be a valid email address.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(12)
            .MaximumLength(128)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one number.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.")
            .Must(NotContainWhitespace).WithMessage("Password must not contain whitespace.");
    }

    private static bool BeValidEmailAddress(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        try
        {
            var address = new MailAddress(email.Trim());
            return string.Equals(address.Address, email.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool NotContainWhitespace(string password)
    {
        return !password.Any(char.IsWhiteSpace);
    }
}

public sealed class LoginRequestDtoValidator : AbstractValidator<LoginRequestDto>
{
    public LoginRequestDtoValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class RefreshTokenRequestDtoValidator : AbstractValidator<RefreshTokenRequestDto>
{
    public RefreshTokenRequestDtoValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public sealed class VerifyRegistrationOtpRequestDtoValidator : AbstractValidator<VerifyRegistrationOtpRequestDto>
{
    public VerifyRegistrationOtpRequestDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(255)
            .Must(BeValidEmailAddress)
            .WithMessage("Email must be a valid email address.");

        RuleFor(x => x.Otp)
            .NotEmpty()
            .Length(6)
            .Matches("^[0-9]+$")
            .WithMessage("OTP must be a 6-digit number.");
    }

    private static bool BeValidEmailAddress(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        try
        {
            var address = new System.Net.Mail.MailAddress(email.Trim());
            return string.Equals(address.Address, email.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}

public sealed class ChangePasswordRequestDtoValidator : AbstractValidator<ChangePasswordRequestDto>
{
    public ChangePasswordRequestDtoValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(12)
            .MaximumLength(128)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one number.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.")
            .Must(NotContainWhitespace).WithMessage("Password must not contain whitespace.");
    }

    private static bool NotContainWhitespace(string password)
    {
        return !password.Any(char.IsWhiteSpace);
    }
}

public sealed class ResetPasswordRequestDtoValidator : AbstractValidator<ResetPasswordRequestDto>
{
    public ResetPasswordRequestDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(255)
            .Must(BeValidEmailAddress)
            .WithMessage("Email must be a valid email address.");

        RuleFor(x => x.Otp)
            .NotEmpty()
            .Length(6)
            .Matches("^[0-9]+$")
            .WithMessage("OTP must be a 6-digit number.");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(12)
            .MaximumLength(128)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one number.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.")
            .Must(NotContainWhitespace).WithMessage("Password must not contain whitespace.");
    }

    private static bool BeValidEmailAddress(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        try
        {
            var address = new MailAddress(email.Trim());
            return string.Equals(address.Address, email.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static bool NotContainWhitespace(string password)
    {
        return !password.Any(char.IsWhiteSpace);
    }
}

public sealed class ForgotPasswordRequestDtoValidator : AbstractValidator<ForgotPasswordRequestDto>
{
    public ForgotPasswordRequestDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(255)
            .Must(BeValidEmailAddress)
            .WithMessage("Email must be a valid email address.");
    }

    private static bool BeValidEmailAddress(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        try
        {
            var address = new MailAddress(email.Trim());
            return string.Equals(address.Address, email.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
}

public sealed class ResendOtpRequestDtoValidator : AbstractValidator<ResendOtpRequestDto>
{
    public ResendOtpRequestDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(255)
            .Must(BeValidEmailAddress)
            .WithMessage("Email must be a valid email address.");
    }

    private static bool BeValidEmailAddress(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        try
        {
            var address = new MailAddress(email.Trim());
            return string.Equals(address.Address, email.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
}
