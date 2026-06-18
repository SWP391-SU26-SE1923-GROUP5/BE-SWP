using AIStudyHub.Business.DTOs.AIChat;
using AIStudyHub.Business.DTOs.Rag;
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
    private readonly IRagChatService _ragChatService;

    public ChatController(IAIChatService chatService, IRagChatService ragChatService)
    {
        _chatService = chatService;
        _ragChatService = ragChatService;
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
        var result = await _chatService.CreateSessionAsync(request);
        return Ok(result);
    }

    [HttpGet("sessions/{sessionId:guid}/messages")]
    public async Task<ActionResult<IReadOnlyList<ChatMessageResponseDto>>> GetMessages(Guid sessionId)
    {
        var result = await _chatService.GetMessagesAsync(sessionId);
        return Ok(result);
    }

    [HttpPost("messages")]
    public async Task<ActionResult<ChatMessageResponseDto>> CreateMessage(CreateChatMessageRequestDto request)
    {
        var result = await _chatService.CreateMessageAsync(request);
        return Ok(result);
    }

    [HttpPost("rag")]
    public async Task<ActionResult<RagChatResponseDto>> RagChat(
        [FromBody] RagChatRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await _ragChatService.ChatAsync(request, userId);
        return Ok(result);
    }

    [HttpPost("summarize")]
    public async Task<ActionResult<SummarizeResponseDto>> Summarize(
        [FromBody] SummarizeRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var summary = await _ragChatService.SummarizeAsync(request.DocumentId, userId);
        return Ok(new SummarizeResponseDto(summary));
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
