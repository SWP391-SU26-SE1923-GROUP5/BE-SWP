using AIStudyHub.Business.DTOs.Users;
using AIStudyHub.Business.Interfaces.Services;
using MediatR;

namespace AIStudyHub.Business.Features.Users.Commands;

public sealed record CreateUserCommand(CreateUserRequestDto Request) : IRequest<UserResponseDto>;

internal sealed class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, UserResponseDto>
{
    private readonly IUserService _userService;

    public CreateUserCommandHandler(IUserService userService)
    {
        _userService = userService;
    }

    public Task<UserResponseDto> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        return _userService.CreateAsync(request.Request, cancellationToken);
    }
}
