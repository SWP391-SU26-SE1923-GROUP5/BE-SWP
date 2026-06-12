namespace AIStudyHub.Business.DTOs.Authentication;

public sealed record RegisterResultDto(
    string Message,
    string Email);

public sealed record ResendOtpRequestDto(string Email);
