using AIStudyHub.Business.DTOs.Questions;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class QuestionController : CrudControllerBase<QuestionResponseDto, CreateQuestionRequestDto, UpdateQuestionRequestDto>
{
    public QuestionController(IQuestionService service)
        : base(service)
    {
    }
}
