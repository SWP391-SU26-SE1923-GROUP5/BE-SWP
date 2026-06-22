using AIStudyHub.Business.DTOs.Users;
using AIStudyHub.Business.Interfaces.Services;
using MediatR;

namespace AIStudyHub.Business.Features.Users.Commands;

public sealed record UpdateUserCommand(Guid Id, UpdateUserRequestDto Request) : IRequest<UserResponseDto>;

internal sealed class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, UserResponseDto>
{
    private readonly IUserService _userService;

    public UpdateUserCommandHandler(IUserService userService)
    {
        _userService = userService;
    }

    public Task<UserResponseDto> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        return _userService.UpdateAsync(request.Id, request.Request, cancellationToken);
    }
}
