namespace AIStudyHub.Business.DTOs.AIChat;

public sealed record ChatSessionResponseDto(Guid Id, Guid UserId, string SessionTitle, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record CreateChatSessionRequestDto(string SessionTitle);

public sealed record ChatMessageResponseDto(Guid Id, Guid ChatSessionId, string Sender, string Content, DateTime CreatedAt, DateTime? UpdatedAt, bool IsRelevant, IReadOnlyList<ChatCitationDto>? Citations = null);

/// <summary>
/// Represents a single citation source from the RAG pipeline.
/// FE uses DocumentId to identify the document, PageNumber to navigate the viewer, and Snippet to highlight text.
/// Source (fileName) is kept as a display-friendly label but may not be unique.
/// </summary>
public sealed record ChatCitationDto(
    Guid DocumentId,
    string Source,
    string Snippet,
    int? PageNumber,
    double Relevance,
    string MatchType,
    bool IsHighlightable = false,
    string? Reason = "legacy_unclassified");

public sealed record CreateChatMessageRequestDto(Guid? SessionId, string Message);

public sealed record AddDocumentToSessionRequestDto(Guid DocumentId);

public sealed record ChatSessionDocumentResponseDto(Guid ChatSessionId, Guid DocumentId, string Title, string? FileName, DateTime AddedAt);
