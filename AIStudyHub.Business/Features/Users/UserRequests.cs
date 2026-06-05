using AIStudyHub.Business.DTOs.Users;
using AIStudyHub.Business.Interfaces.Services;
using MediatR;

namespace AIStudyHub.Business.Features.Users;

public sealed record GetUsersQuery : IRequest<IReadOnlyList<UserResponseDto>>;

public sealed record GetUserByIdQuery(Guid Id) : IRequest<UserResponseDto?>;

public sealed record CreateUserCommand(CreateUserRequestDto Request) : IRequest<UserResponseDto>;

public sealed record UpdateUserCommand(Guid Id, UpdateUserRequestDto Request) : IRequest<UserResponseDto>;

public sealed record DeleteUserCommand(Guid Id) : IRequest;

internal sealed class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, IReadOnlyList<UserResponseDto>>
{
    private readonly IUserService _userService;

    public GetUsersQueryHandler(IUserService userService)
    {
        _userService = userService;
    }

    public Task<IReadOnlyList<UserResponseDto>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        return _userService.GetAllAsync(cancellationToken);
    }
}

internal sealed class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserResponseDto?>
{
    private readonly IUserService _userService;

    public GetUserByIdQueryHandler(IUserService userService)
    {
        _userService = userService;
    }

    public Task<UserResponseDto?> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        return _userService.GetByIdAsync(request.Id, cancellationToken);
    }
}

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

internal sealed class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand>
{
    private readonly IUserService _userService;

    public DeleteUserCommandHandler(IUserService userService)
    {
        _userService = userService;
    }

    public async Task Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        await _userService.DeleteAsync(request.Id, cancellationToken);
    }
}
