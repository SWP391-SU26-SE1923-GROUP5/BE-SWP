using AIStudyHub.Business.DTOs.AIChat;
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
    public async Task<ActionResult<IReadOnlyList<ChatSessionResponseDto>>> GetSessions(CancellationToken cancellationToken)
    {
        var result = await _chatService.GetSessionsAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("sessions")]
    public async Task<ActionResult<ChatSessionResponseDto>> CreateSession(CreateChatSessionRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _chatService.CreateSessionAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("sessions/{sessionId:guid}/messages")]
    public async Task<ActionResult<IReadOnlyList<ChatMessageResponseDto>>> GetMessages(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _chatService.GetMessagesAsync(sessionId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("messages")]
    public async Task<ActionResult<ChatMessageResponseDto>> CreateMessage(CreateChatMessageRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _chatService.CreateMessageAsync(request, cancellationToken);
        return Ok(result);
    }
}
