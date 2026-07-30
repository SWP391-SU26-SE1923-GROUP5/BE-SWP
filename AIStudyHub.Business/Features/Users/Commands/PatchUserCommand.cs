using AIStudyHub.Business.DTOs.Users;
using AIStudyHub.Business.Interfaces.Services;
using MediatR;

namespace AIStudyHub.Business.Features.Users.Commands;

public sealed record PatchUserCommand(Guid Id, PatchUserRequestDto Request) : IRequest<UserResponseDto>;

internal sealed class PatchUserCommandHandler : IRequestHandler<PatchUserCommand, UserResponseDto>
{
    private readonly IUserService _userService;

    public PatchUserCommandHandler(IUserService userService)
    {
        _userService = userService;
    }

    public async Task<UserResponseDto> Handle(PatchUserCommand request, CancellationToken cancellationToken)
    {
        var existing = await _userService.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("User not found.");

        var patched = new UpdateUserRequestDto(
            request.Request.FullName ?? existing.FullName,
            request.Request.DateOfBirth ?? existing.DateOfBirth,
            request.Request.CurrentStorageCapacity ?? existing.CurrentStorageCapacity,
            existing.CurrentAiTokenUsage,
            request.Request.Status ?? existing.Status,
            request.Request.Role ?? existing.Role);

        return await _userService.UpdateAsync(request.Id, patched, cancellationToken);
    }
}
