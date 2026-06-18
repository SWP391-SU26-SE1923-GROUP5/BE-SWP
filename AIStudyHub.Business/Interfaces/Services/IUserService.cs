using AIStudyHub.Business.DTOs.Users;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IUserService : ICrudService<UserResponseDto, CreateUserRequestDto, UpdateUserRequestDto>
{
    Task<UserTierInfoDto?> GetUserTierInfoAsync(Guid userId, CancellationToken cancellationToken = default);
    Task UpdateUserTierAsync(Guid userId, UpdateUserTierRequestDto request, CancellationToken cancellationToken = default);
    Task UpdateProfileAsync(Guid userId, UpdateProfileRequestDto request, CancellationToken cancellationToken = default);
}
