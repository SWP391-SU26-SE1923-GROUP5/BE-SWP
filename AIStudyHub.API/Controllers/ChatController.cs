using AIStudyHub.Business.DTOs.AIChat;
using AIStudyHub.Business.DTOs.Rag;
using AIStudyHub.Business.Interfaces.AI.Chat;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class ChatController : ControllerBase
{
    private readonly IAIChatService _chatService;

    public ChatController(IAIChatService chatService)
    {
        _chatService = chatService;
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<IReadOnlyList<ChatSessionResponseDto>>> GetSessions()
    {
        var result = await _chatService.GetSessionsAsync();
        return Ok(result);
    }

    [HttpPost("sessions")]
    public async Task<ActionResult<ChatSessionResponseDto>> CreateSession(CreateChatSessionRequestDto request)
    {
        var userId = GetCurrentUserId();
        var result = await _chatService.CreateSessionAsync(request, userId);
        return Ok(result);
    }

    [HttpPut("sessions/{sessionId:guid}")]
    public async Task<ActionResult<ChatSessionResponseDto>> UpdateSession(
        Guid sessionId,
        [FromBody] UpdateChatSessionRequestDto request,
        CancellationToken ct)
    {
        try
        {
            var result = await _chatService.UpdateSessionAsync(sessionId, request, GetCurrentUserId(), ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpDelete("sessions/{sessionId:guid}")]
    public async Task<IActionResult> DeleteSession(Guid sessionId, CancellationToken ct)
    {
        try
        {
            await _chatService.DeleteSessionAsync(sessionId, GetCurrentUserId(), ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("sessions/{sessionId:guid}/messages")]
    public async Task<ActionResult<IReadOnlyList<ChatMessageResponseDto>>> GetMessages(Guid sessionId)
    {
        try
        {
            var result = await _chatService.GetMessagesAsync(sessionId, GetCurrentUserId());
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost("messages")]
    public async Task<ActionResult<ChatMessageResponseDto>> CreateMessage(CreateChatMessageRequestDto request)
    {
        var userId = GetCurrentUserId();
        var result = await _chatService.CreateMessageAsync(request, userId);
        return Ok(result);
    }

    [HttpGet("sessions/{sessionId:guid}/documents")]
    public async Task<ActionResult<IReadOnlyList<ChatSessionDocumentResponseDto>>> GetDocuments(Guid sessionId)
    {
        var userId = GetCurrentUserId();
        try
        {
            var result = await _chatService.GetDocumentsAsync(sessionId, userId);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("sessions/{sessionId:guid}/documents")]
    public async Task<ActionResult<ChatSessionDocumentResponseDto>> AddDocument(Guid sessionId, [FromBody] AddDocumentToSessionRequestDto request)
    {
        var userId = GetCurrentUserId();
        try
        {
            var result = await _chatService.AddDocumentAsync(sessionId, request.DocumentId, userId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpDelete("sessions/{sessionId:guid}/documents/{documentId:guid}")]
    public async Task<IActionResult> RemoveDocument(Guid sessionId, Guid documentId)
    {
        var userId = GetCurrentUserId();
        try
        {
            await _chatService.RemoveDocumentAsync(sessionId, documentId, userId);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub")
            ?? User.FindFirst("userId");

        return claim != null && Guid.TryParse(claim.Value, out var userId)
            ? userId
            : Guid.Empty;
    }
}
