namespace AIStudyHub.Business.DTOs.Authentication;

public sealed record ExternalLoginRequestDto(string Provider, string Email, string FullName);
