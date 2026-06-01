using AIStudyHub.Business.DTOs.Users;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[Route("api/[controller]")]
public sealed class UserController : CrudControllerBase<UserResponseDto, CreateUserRequestDto, UpdateUserRequestDto>
{
    public UserController(IUserService service)
        : base(service)
    {
    }
}
