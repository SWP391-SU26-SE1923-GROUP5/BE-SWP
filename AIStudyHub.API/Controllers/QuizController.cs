using AIStudyHub.Business.DTOs.Questions;
using AIStudyHub.Business.DTOs.Answers;
using AIStudyHub.Business.DTOs.Quizzes;
using AIStudyHub.Business.DTOs.QuizSubmissions;
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
    private readonly IQuizSubmissionService _submissionService;

    public QuizController(
        IQuizService service,
        IDocumentService documentService,
        IQuestionService questionService,
        IAnswerService answerService,
        IQuizSubmissionService submissionService)
    {
        _service = service;
        _documentService = documentService;
        _questionService = questionService;
        _answerService = answerService;
        _submissionService = submissionService;
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

    /// <summary>
    /// Plan C3 / B.4.5 — submit a quiz attempt for grading.
    /// The quiz id is taken from the route; <c>UserId</c> and <c>QuizId</c> in the body
    /// are ignored and replaced with server-side values to prevent spoofing.
    /// Returns the saved submission plus any badges the user just unlocked.
    /// </summary>
    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<SubmitQuizResultDto>> Submit(
        Guid id,
        [FromBody] CreateQuizSubmissionRequestDto request,
        CancellationToken cancellationToken)
    {
        var quiz = await _service.GetByIdAsync(id, cancellationToken);
        if (quiz == null) return NotFound("Quiz not found");

        var document = await _documentService.GetByIdAsync(quiz.DocumentId, cancellationToken);
        if (document == null) return NotFound("Document not found");

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Answers))
            return BadRequest("Answers payload is required");

        // Override client-supplied identifiers with server-side values to prevent
        // a user from submitting as someone else or against an unrelated quiz.
        var serverRequest = request with { UserId = userId, QuizId = id };

        try
        {
            var result = await _submissionService.SubmitAsync(serverRequest, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception)
        {
            return StatusCode(500, "An error occurred while submitting the quiz");
        }
    }

    /// <summary>Lấy lịch sử nộp bài của tất cả user theo quiz.</summary>
    [HttpGet("{quizId:guid}/history")]
    public async Task<ActionResult<AIStudyHub.Business.DTOs.Common.PagedResultDto<QuizSubmissionHistoryDto>>> GetHistory(
        Guid quizId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        var quiz = await _service.GetByIdAsync(quizId, ct);
        if (quiz == null) return NotFound("Quiz not found");

        var @params = new AIStudyHub.Business.DTOs.Common.PaginationParams { Offset = offset, Limit = limit };
        var result = await _submissionService.GetQuizHistoryAsync(quizId, fromDate, toDate, @params, ct);
        return Ok(result);
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub" || c.Type == "userId")?.Value;
        return claim != null && Guid.TryParse(claim, out var userId) ? userId : Guid.Empty;
    }
}
