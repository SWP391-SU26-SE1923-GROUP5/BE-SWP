using AIStudyHub.Business.DTOs.Users;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IUserService : ICrudService<UserResponseDto, CreateUserRequestDto, UpdateUserRequestDto>
{
    Task<UserTierInfoDto?> GetUserTierInfoAsync(Guid userId, CancellationToken cancellationToken = default);
    Task UpdateUserTierAsync(Guid userId, UpdateUserTierRequestDto request, CancellationToken cancellationToken = default);
    Task UpdateProfileAsync(Guid userId, UpdateProfileRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns active users available as share targets, excluding the caller.
    /// Supports an optional <paramref name="keyword"/> to filter by full name or email.
    /// </summary>
    Task<IReadOnlyList<ShareableUserDto>> GetShareableUsersAsync(
        Guid callerId,
        string? keyword = null,
        CancellationToken cancellationToken = default);
}
