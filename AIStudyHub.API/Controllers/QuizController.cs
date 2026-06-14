using AIStudyHub.Business.DTOs.Quizzes;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Collections.Generic;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class QuizController : ControllerBase
{
    private readonly IQuizService _service;

    public QuizController(IQuizService service)
    {
        _service = service;
    }

    /// <summary>Lấy danh sách tất cả quiz.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<QuizResponseDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("/api/quiz/document/{docId:guid}/ai-gen")]
    public async Task<ActionResult<AiGeneratedQuizResponseDto>> GenerateFromDocument(
        Guid docId,
        [FromBody] AIStudyHub.Business.DTOs.Quizzes.CreateQuizRequestViaAIDto request,
        [FromServices] AIStudyHub.Data.Interfaces.IUnitOfWork unitOfWork,
        [FromServices] AIStudyHub.Business.Interfaces.Services.IRagChatService ragChatService,
        CancellationToken cancellationToken)
    {
        // Basic auth check
        var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub" || c.Type == "userId")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Forbid();

        // Use the message from the simple DTO
        var message = request.Message ?? string.Empty;

        // Try to extract requested number of questions from the message (e.g., "give me 20 questions").
        // Default to 10 if not specified, enforce max 20.
        var numberOfQuestions = 10;
        try
        {
            var m = System.Text.RegularExpressions.Regex.Match(message, "\\b(\\d{1,2})\\b");
            if (m.Success && int.TryParse(m.Groups[1].Value, out var parsed))
                numberOfQuestions = Math.Clamp(parsed, 1, 20);
        }
        catch { /* ignore regex errors and use default */ }

        // Load document
        var document = await unitOfWork.Documents.GetByIdAsync(docId, cancellationToken);
        if (document is null)
            return NotFound("Document not found.");

        // Load chunks (if available)
        var chunks = await unitOfWork.DocumentChunks.Query()
            .Where(c => c.DocumentId == docId)
            .OrderBy(c => c.OrderIndex)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Build prompt instructing model to return strict JSON
        var constraintText = string.Empty; // CreateQuizRequestViaAIDto only contains Message; constraints must be expressed inside the message if needed

        var instruction = "Return ONLY a single JSON object exactly matching this schema: {\"quizTitle\":string, \"questions\":[{\"questionTitle\":string, \"questionType\":\"SingleChoice|MultipleChoice|TrueFalse\", \"position\":int, \"answers\":[{\"selectedOption\":string, \"isCorrect\":boolean}]}]} with no additional text.";

        var promptBuilder = new System.Text.StringBuilder();
        promptBuilder.AppendLine(instruction);
        if (!string.IsNullOrWhiteSpace(constraintText))
            promptBuilder.AppendLine($"Constraints: {constraintText}");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("User message:");
        promptBuilder.AppendLine(message);
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Context:");
        foreach (var c in chunks)
        {
            if (!string.IsNullOrWhiteSpace(c.ChunkJson))
            {
                promptBuilder.AppendLine(c.ChunkJson);
                promptBuilder.AppendLine();
            }
            if (promptBuilder.Length > 30_000) break;
        }

        // RagChatRequestDto overload accepts optional single document id
        var ragRequest = new AIStudyHub.Business.DTOs.Rag.RagChatRequestDto(promptBuilder.ToString(), docId);
        var ragResponse = await ragChatService.ChatAsync(ragRequest, userId);
        var aiText = ragResponse.Answer ?? string.Empty;
        
        // Try parse JSON
        AIStudyHub.Business.DTOs.Quizzes.AiGeneratedQuizResponseDto? aiResult = null;
        try
        {
            var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            aiResult = System.Text.Json.JsonSerializer.Deserialize<AIStudyHub.Business.DTOs.Quizzes.AiGeneratedQuizResponseDto>(aiText, options);
        }
        catch (System.Text.Json.JsonException)
        {
            return BadRequest("AI returned invalid JSON. Please ensure the prompt requests JSON output.");
        }

        if (aiResult is null || aiResult.Questions is null || aiResult.Questions.Count == 0)
            return BadRequest("AI did not return any questions.");

        // Persist quiz
        var quiz = new AIStudyHub.Data.Entities.Quiz { DocumentId = docId, Title = aiResult.QuizTitle ?? document.Title ?? "Generated Quiz" };
        await unitOfWork.Quizzes.AddAsync(quiz, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var q in aiResult.Questions.Take(numberOfQuestions))
        {
            // QuestionType is already an enum on the DTO; use it directly.
            var qt = q.QuestionType;

            var question = new AIStudyHub.Data.Entities.Question
            {
                QuizId = quiz.Id,
                Title = q.QuestionTitle,
                Type = qt,
                Position = q.Position > 0 ? q.Position : 0
            };

            await unitOfWork.Questions.AddAsync(question, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            if (q.Answers != null)
            {
                foreach (var a in q.Answers)
                {
                    var answer = new AIStudyHub.Data.Entities.Answer
                    {
                        QuestionId = question.Id,
                        SelectedOption = a.SelectedOption,
                        IsCorrect = a.IsCorrect
                    };
                    await unitOfWork.Answers.AddAsync(answer, cancellationToken);
                }
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        return Ok(aiResult);
    }
    /// <summary>Lấy thông tin quiz theo ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<QuizResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    // POST   /api/Quiz  - Đã xóa. Quiz phải được AI sinh ra từ Document.
    // PUT    /api/Quiz/{id} - Đã xóa.
    // DELETE /api/Quiz/{id} - Đã xóa. Xóa quiz phải đi kèm xóa Question và Answer con.
}
