using AIStudyHub.Business.DTOs.Users;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IUserService : ICrudService<UserResponseDto, CreateUserRequestDto, UpdateUserRequestDto>
{
}
