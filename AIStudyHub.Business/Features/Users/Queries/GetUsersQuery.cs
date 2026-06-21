using AIStudyHub.Business.DTOs.Users;
using AIStudyHub.Business.Interfaces.Services;
using MediatR;

namespace AIStudyHub.Business.Features.Users.Queries;

public sealed record GetUsersQuery : IRequest<IReadOnlyList<UserResponseDto>>;

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
