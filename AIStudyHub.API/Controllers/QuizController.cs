using AIStudyHub.Business.DTOs.Quizzes;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Data.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class QuizController : ControllerBase
{
    private readonly IQuizService _service;
    private readonly IQuizAiService _quizAiService;

    public QuizController(IQuizService service, IQuizAiService quizAiService)
    {
        _service = service;
        _quizAiService = quizAiService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<QuizResponseDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("/api/quiz/document/{docId:guid}/ai-gen")]
    public async Task<ActionResult<AiGeneratedQuizResponseDto>> GenerateFromDocument(
        Guid docId,
        [FromBody] CreateQuizRequestViaAIDto dto,
        [FromServices] AIStudyHub.Data.Interfaces.IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.Claims.FirstOrDefault(c =>
            c.Type == System.Security.Claims.ClaimTypes.NameIdentifier
            || c.Type == "sub"
            || c.Type == "userId")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Forbid();

        if (dto.numberOfQuestions <= 0 || dto.numberOfQuestions > 20)
            return BadRequest("Number of questions must be between 1 and 20.");

        AiGeneratedQuizResponseDto aiResult;
        try
        {
            aiResult = await _quizAiService.GenerateQuizAsync(docId, dto, userId, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Document not found.");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        var cleanedQuestions = aiResult.Questions
            .Where(q => !q.QuestionTitle?.StartsWith("__SKIP__") ?? false)
            .Select(q =>
            {
                var cleanedTitle = string.IsNullOrWhiteSpace(q.QuestionTitle)
                    ? string.Empty
                    : Regex.Replace(q.QuestionTitle, "\\s*\\[\\d+\\]", string.Empty).Trim();

                var validTypes = new HashSet<QuestionType>
                    { QuestionType.SingleChoice, QuestionType.MultipleChoice, QuestionType.TrueFalse };
                var qt = validTypes.Contains(q.QuestionType) ? q.QuestionType : QuestionType.SingleChoice;
                var answerCount = qt == QuestionType.TrueFalse ? 2 : 4;

                var answers = (q.Answers ?? Enumerable.Empty<AiGeneratedAnswerDto>())
                    .Take(answerCount)
                    .Select(a =>
                    {
                        var opt = string.IsNullOrWhiteSpace(a.SelectedOption)
                            ? string.Empty
                            : Regex.Replace(a.SelectedOption, "\\s*\\[\\d+\\]", string.Empty).Trim();
                        return new AiGeneratedAnswerDto(opt, a.IsCorrect);
                    }).ToList();

                return new AiGeneratedQuestionDto(cleanedTitle, qt, q.Position, answers);
            }).ToList();

        if (cleanedQuestions.Count == 0)
            return BadRequest("AI did not generate any usable questions.");

        var cleanedTitle = string.IsNullOrWhiteSpace(aiResult.QuizTitle)
            ? string.Empty
            : Regex.Replace(aiResult.QuizTitle, "\\s*\\[\\d+\\]", string.Empty).Trim();

        var quiz = new AIStudyHub.Data.Entities.Quiz
        {
            DocumentId = docId,
            Title = string.IsNullOrWhiteSpace(cleanedTitle) ? "Generated Quiz" : cleanedTitle
        };
        await unitOfWork.Quizzes.AddAsync(quiz, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var q in cleanedQuestions)
        {
            var questionEntity = new AIStudyHub.Data.Entities.Question
            {
                QuizId = quiz.Id,
                Title = q.QuestionTitle ?? string.Empty,
                Type = q.QuestionType,
                Position = q.Position > 0 ? q.Position : 0
            };
            await unitOfWork.Questions.AddAsync(questionEntity, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            foreach (var a in q.Answers ?? Enumerable.Empty<AiGeneratedAnswerDto>())
            {
                var answerEntity = new AIStudyHub.Data.Entities.Answer
                {
                    QuestionId = questionEntity.Id,
                    SelectedOption = string.IsNullOrWhiteSpace(a.SelectedOption)
                        ? string.Empty
                        : Regex.Replace(a.SelectedOption, "\\s*\\[\\d+\\]", string.Empty).Trim(),
                    IsCorrect = a.IsCorrect
                };
                await unitOfWork.Answers.AddAsync(answerEntity, cancellationToken);
            }
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var response = new AiGeneratedQuizResponseDto(cleanedTitle, cleanedQuestions);
        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<QuizResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<QuizResponseDto>> Create([FromBody] CreateQuizRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<QuizResponseDto>> Update(Guid id, [FromBody] UpdateQuizRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _service.UpdateAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
