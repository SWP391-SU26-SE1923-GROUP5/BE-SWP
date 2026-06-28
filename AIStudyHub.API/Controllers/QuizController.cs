using AIStudyHub.Business.DTOs.Questions;
using AIStudyHub.Business.DTOs.Answers;
using AIStudyHub.Business.DTOs.Quizzes;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class QuizController : ControllerBase
{
    private readonly IQuizService _service;
    private readonly IDocumentService _documentService;
    private readonly IQuestionService _questionService;
    private readonly IAnswerService _answerService;

    public QuizController(
        IQuizService service,
        IDocumentService documentService,
        IQuestionService questionService,
        IAnswerService answerService)
    {
        _service = service;
        _documentService = documentService;
        _questionService = questionService;
        _answerService = answerService;
    }

    [HttpGet]
    public async Task<ActionResult<AIStudyHub.Business.DTOs.Common.PagedResultDto<QuizResponseDto>>> GetAll([FromQuery] AIStudyHub.Business.DTOs.Common.PaginationParams @params, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await _service.GetAllPagedAsync(@params, userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>Lấy thông tin quiz theo ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<QuizResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        if (result == null) return NotFound();

        var document = await _documentService.GetByIdAsync(result.DocumentId, cancellationToken);
        if (document == null) return NotFound();

        var userId = GetCurrentUserId();
        if (document.UserId != userId && document.ShareStatus != "public") return Forbid();

        return Ok(result);
    }

    /// <summary>Lấy danh sách câu hỏi của một quiz.</summary>
    [HttpGet("{id:guid}/questions")]
    public async Task<ActionResult<IReadOnlyList<QuestionResponseDto>>> GetQuestions(Guid id, CancellationToken cancellationToken)
    {
        var quiz = await _service.GetByIdAsync(id, cancellationToken);
        if (quiz == null) return NotFound();

        var document = await _documentService.GetByIdAsync(quiz.DocumentId, cancellationToken);
        if (document == null) return NotFound();

        var userId = GetCurrentUserId();
        if (document.UserId != userId && document.ShareStatus != "public") return Forbid();

        var result = await _questionService.GetByQuizIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>Lấy thông tin câu hỏi.</summary>
    [HttpGet("{quizId:guid}/questions/{questionId:guid}")]
    public async Task<ActionResult<QuestionResponseDto>> GetQuestion(Guid quizId, Guid questionId, CancellationToken cancellationToken)
    {
        var quiz = await _service.GetByIdAsync(quizId, cancellationToken);
        if (quiz == null) return NotFound();

        var document = await _documentService.GetByIdAsync(quiz.DocumentId, cancellationToken);
        if (document == null) return NotFound();

        var userId = GetCurrentUserId();
        if (document.UserId != userId && document.ShareStatus != "public") return Forbid();

        var result = await _questionService.GetByIdAsync(questionId, cancellationToken);
        if (result == null || result.QuizId != quizId) return NotFound();

        return Ok(result);
    }

    /// <summary>Lấy danh sách câu trả lời của một câu hỏi.</summary>
    [HttpGet("{quizId:guid}/questions/{questionId:guid}/answers")]
    public async Task<ActionResult<IReadOnlyList<AnswerResponseDto>>> GetAnswers(Guid quizId, Guid questionId, CancellationToken cancellationToken)
    {
        var quiz = await _service.GetByIdAsync(quizId, cancellationToken);
        if (quiz == null) return NotFound();

        var document = await _documentService.GetByIdAsync(quiz.DocumentId, cancellationToken);
        if (document == null) return NotFound();

        var userId = GetCurrentUserId();
        if (document.UserId != userId && document.ShareStatus != "public") return Forbid();

        var question = await _questionService.GetByIdAsync(questionId, cancellationToken);
        if (question == null || question.QuizId != quizId) return NotFound();

        var result = await _answerService.GetByQuestionIdAsync(questionId, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var quiz = await _service.GetByIdAsync(id, cancellationToken);
        if (quiz == null) return NotFound();

        var document = await _documentService.GetByIdAsync(quiz.DocumentId, cancellationToken);
        if (document == null) return NotFound();

        var userId = GetCurrentUserId();
        if (document.UserId != userId) return Forbid();

        await _service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub" || c.Type == "userId")?.Value;
        return claim != null && Guid.TryParse(claim, out var userId) ? userId : Guid.Empty;
    }
}
