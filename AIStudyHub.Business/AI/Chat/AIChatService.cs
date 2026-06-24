using AIStudyHub.Business.AI.LLM;
using AIStudyHub.Business.AI.Chat;
using AIStudyHub.Business.Interfaces.AI.Chat;
using AIStudyHub.Business.Interfaces.AI.LLM;
using AIStudyHub.Business.DTOs.AIChat;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Builder;
using AIStudyHub.Business.Interfaces.AI.Orchestration;
using Microsoft.EntityFrameworkCore;

namespace AIStudyHub.Business.AI.Chat;

public sealed class AIChatService : IAIChatService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILocalAIService _localAIService;
    private readonly ISemanticKernelOrchestrator _orchestrator;
    
    public AIChatService(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILocalAIService openAiService,
        ISemanticKernelOrchestrator orchestrator)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _localAIService = openAiService;
        _orchestrator = orchestrator;
    }

    public async Task<IReadOnlyList<ChatSessionResponseDto>> GetSessionsAsync()
    {
        var sessions = await _unitOfWork.ChatSessions
            .Query()
            .Include(session => session.User)
            .Include(session => session.Document)
            .AsNoTracking()
            .OrderByDescending(session => session.CreatedAt)
            .ToListAsync();

        return sessions.Select(_mapper.Map<ChatSessionResponseDto>).ToList();
    }

    public async Task<ChatSessionResponseDto> CreateSessionAsync(CreateChatSessionRequestDto request, Guid userId)
    {
        var session = new ChatSession
        {
            UserId = userId,
            SessionTitle = request.SessionTitle
        };
        await _unitOfWork.ChatSessions.AddAsync(session);
        await _unitOfWork.SaveChangesAsync();

        var created = await _unitOfWork.ChatSessions
            .Query()
            .Include(chatSession => chatSession.User)
            .Include(chatSession => chatSession.Document)
            .AsNoTracking()
            .FirstAsync(chatSession => chatSession.Id == session.Id);

        return _mapper.Map<ChatSessionResponseDto>(created);
    }

    public async Task<IReadOnlyList<ChatMessageResponseDto>> GetMessagesAsync(Guid sessionId)
    {
        var sessionExists = await _unitOfWork.ChatSessions.GetByIdAsync(sessionId) is not null;
        if (!sessionExists)
        {
            throw new KeyNotFoundException($"Chat session with ID {sessionId} not found.");
        }

        var messages = await _unitOfWork.ChatMessages
            .Query()
            .Where(message => message.ChatSessionId == sessionId)
            .OrderBy(message => message.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        return messages.Select(_mapper.Map<ChatMessageResponseDto>).ToList();
    }

    public async Task<ChatMessageResponseDto> CreateMessageAsync(CreateChatMessageRequestDto request, Guid userId, CancellationToken ct = default)
    {
        ChatSession session;
        if (!request.SessionId.HasValue)
        {
            var title = request.Message.Length > 50 ? request.Message.Substring(0, 47) + "..." : request.Message;
            session = new ChatSession
            {
                UserId = userId,
                DocumentId = request.DocumentId,
                SessionTitle = title
            };
            await _unitOfWork.ChatSessions.AddAsync(session);
            await _unitOfWork.SaveChangesAsync();
        }
        else
        {
            session = await _unitOfWork.ChatSessions.GetByIdAsync(request.SessionId.Value);
            if (session == null || session.UserId != userId)
            {
                throw new KeyNotFoundException($"Chat session with ID {request.SessionId} not found or access denied.");
            }

            if (request.DocumentId.HasValue && session.DocumentId == null)
            {
                session.DocumentId = request.DocumentId.Value;
                _unitOfWork.ChatSessions.Update(session);
                await _unitOfWork.SaveChangesAsync();
            }
        }

        var userMessage = new ChatMessage
        {
            ChatSessionId = session.Id,
            Sender = "user",
            Content = request.Message
        };
        await _unitOfWork.ChatMessages.AddAsync(userMessage);
        await _unitOfWork.SaveChangesAsync();

        var history = await _unitOfWork.ChatMessages
            .Query()
            .Where(m => m.ChatSessionId == session.Id)
            .OrderByDescending(m => m.CreatedAt)
            .Take(10)
            .ToListAsync(ct);
        history.Reverse();

        var activeDocumentId = request.DocumentId ?? session.DocumentId;
        string aiResponse;

        if (activeDocumentId.HasValue)
        {
            var ragResponse = await _orchestrator.AskAsync(userId, activeDocumentId.Value, request.Message, history, ct);
            aiResponse = ragResponse.Answer;
        }
        else
        {
            var historyText = string.Join("\n", history.Select(m => $"{m.Sender}: {m.Content}"));
            var prompt = $"CHAT HISTORY:\n{historyText}\n\nUSER: {request.Message}\nASSISTANT:";
            aiResponse = await _localAIService.SendMessageAsync(prompt) ?? "Xin lỗi, tôi không thể trả lời lúc này.";
        }

        var assistantMessage = new ChatMessage
        {
            ChatSessionId = session.Id,
            Sender = "assistant",
            Content = aiResponse
        };

        await _unitOfWork.ChatMessages.AddAsync(assistantMessage);
        await _unitOfWork.SaveChangesAsync();

        var created = await _unitOfWork.ChatMessages
            .Query()
            .AsNoTracking()
            .FirstAsync(chatMessage => chatMessage.Id == assistantMessage.Id, ct);

        return _mapper.Map<ChatMessageResponseDto>(created);
    }
}
