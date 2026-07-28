namespace AIStudyHub.Business.DTOs.AIChat;

/// <summary>
/// Full citation entity stored in the database — contains all RAG metadata.
/// </summary>
public sealed record ChatCitationDto(
    int CitationIndex,
    Guid DocumentId,
    string Source,
    string Snippet,
    int? PageNumber,
    double Relevance,
    string MatchType,
    bool IsHighlightable = false,
    string? Reason = "legacy_unclassified");

/// <summary>
/// Lightweight citation returned to the frontend — contains only what the UI needs.
/// </summary>
public sealed record ChatCitationResponseDto(
    int CitationIndex,
    Guid DocumentId,
    string Snippet,
    int? PageNumber);

public sealed record ChatSessionResponseDto(Guid Id, Guid UserId, string SessionTitle, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record CreateChatSessionRequestDto(string SessionTitle);

public sealed record UpdateChatSessionRequestDto(string SessionTitle);

public sealed record ChatMessageResponseDto(Guid Id, Guid ChatSessionId, string Sender, string Content, DateTime CreatedAt, DateTime? UpdatedAt, bool IsRelevant, IReadOnlyList<ChatCitationResponseDto> Citations);

public sealed record CreateChatMessageRequestDto(Guid? SessionId, string Message);

public sealed record AddDocumentToSessionRequestDto(Guid DocumentId);

public sealed record ChatSessionDocumentResponseDto(Guid ChatSessionId, Guid DocumentId, string Title, string? FileName, DateTime AddedAt);
