using AIStudyHub.Business.DTOs.QuizSubmissions;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class QuizSubmissionController : CrudControllerBase<QuizSubmissionResponseDto, CreateQuizSubmissionRequestDto, UpdateQuizSubmissionRequestDto>
{
    public QuizSubmissionController(IQuizSubmissionService service)
        : base(service)
    {
    }
}
