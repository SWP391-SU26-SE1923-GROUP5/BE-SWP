using AIStudyHub.Business.DTOs.Answers;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class AnswerController : CrudControllerBase<AnswerResponseDto, CreateAnswerRequestDto, UpdateAnswerRequestDto>
{
    public AnswerController(IAnswerService service)
        : base(service)
    {
    }
}
