namespace AIStudyHub.Business.DTOs.Authentication;

public sealed record RegisterResultDto(
    string Message,
    string Email);

public sealed record ConfirmEmailRequestDto(
    string UserId,
    string Token);

public sealed record ResendEmailVerificationRequestDto(string Email);
