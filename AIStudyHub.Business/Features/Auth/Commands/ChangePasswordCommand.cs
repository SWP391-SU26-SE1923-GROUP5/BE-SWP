using AIStudyHub.Business.DTOs.Authentication;
using AIStudyHub.Business.Interfaces.Services;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AIStudyHub.Business.Features.Auth.Commands;

public sealed record ChangePasswordCommand(ChangePasswordRequestDto Request) : IRequest;

internal sealed class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand>
{
    private readonly IAuthService _authService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ChangePasswordCommandHandler(IAuthService authService, IHttpContextAccessor httpContextAccessor)
    {
        _authService = authService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        await _authService.ChangePasswordAsync(user!, request.Request, cancellationToken);
    }
}
