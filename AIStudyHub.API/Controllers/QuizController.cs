using AIStudyHub.Business.DTOs.Quizzes;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[Route("api/[controller]")]
public sealed class QuizController : CrudControllerBase<QuizResponseDto, CreateQuizRequestDto, UpdateQuizRequestDto>
{
    public QuizController(IQuizService service)
        : base(service)
    {
    }
}
