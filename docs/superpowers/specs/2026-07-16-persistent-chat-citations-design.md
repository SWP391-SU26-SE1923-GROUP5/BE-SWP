# Persistent Chat Citations Design

> **Superseded:** This historical design is replaced by the [2026-07-30 mentor backend remediation design](2026-07-30-mentor-backend-remediation-design.md). Its original body is retained for migration history and context.

## Goal

Persist the citations attached to assistant chat messages so citation deep-links continue to work after a page refresh or a later session reload. This change is Backend-only. The Frontend will use the existing session APIs and place `sessionId` in its route.

## Scope

Included:

- Store an immutable citation snapshot for every assistant message.
- Return stored citations from the existing chat-history endpoint.
- Preserve citation ordering so `[1]`, `[2]`, and later markers resolve consistently.
- Document the API contract the Frontend needs for session routing and viewer navigation.

Excluded:

- Frontend routing or UI implementation.
- Reconstructing historical citations that were created before this migration.
- Re-querying Qdrant when loading chat history.
- Adding a new citation endpoint.

## Current Problem

`AIChatService` builds citations only for the response returned when an assistant message is created. `ChatMessage` does not persist them, and the entity-to-DTO mapping explicitly returns `null` citations. After refresh, the message text still contains citation markers, but the corresponding document, page, and snippet metadata is unavailable.

## Data Model

Add a `ChatMessageCitation` entity and table with a required many-to-one relationship to `ChatMessage`.

| Field | Type | Requirement |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `ChatMessageId` | `Guid` | Required foreign key to `ChatMessage` |
| `CitationIndex` | `int` | Required, one-based marker order |
| `DocumentId` | `Guid` | Required snapshot value; no database foreign key |
| `Source` | `string` | Required file/display name |
| `Snippet` | `string` | Required citation text |
| `PageNumber` | `int?` | Authoritative page when available |
| `Relevance` | `double` | Retrieval relevance snapshot |
| `MatchType` | `string` | Required retrieval match type |
| `IsHighlightable` | `bool` | Whether Viewer text highlighting is supported |
| `Reason` | `string?` | Reason highlighting is unavailable |
| `CreatedAt` | `DateTime` | Citation creation timestamp |

Constraints:

- Unique index on `(ChatMessageId, CitationIndex)`.
- Cascade delete from `ChatMessage` to its citations.
- No foreign key from `DocumentId` to `Document`. A citation is historical evidence and must remain readable if the source document is later deleted.
- Citations are immutable snapshots and are never refreshed from Qdrant during history reads.

`ChatMessage` gains a citation navigation collection. `ApplicationDbContext` and the unit-of-work/repository access required by the existing persistence pattern are updated accordingly.

## Write Flow

1. Store the user message and build chat history as today.
2. Run the RAG orchestration and obtain the assistant answer and ordered citations.
3. Create the assistant `ChatMessage`.
4. Create one `ChatMessageCitation` per returned citation, assigning one-based `CitationIndex` values in response order.
5. Save the assistant message and citations atomically.
6. Return the created assistant message with the same persisted citation snapshots.

The assistant message and citations must share one database transaction. A citation persistence failure rolls back the assistant message so the database cannot retain `[n]` markers without their metadata. User-message persistence behavior remains unchanged unless the current unit-of-work transaction naturally encompasses it.

Messages without citations store no citation rows.

## Read Flow and Authorization

`GET /api/Chat/sessions/{sessionId}/messages` will:

1. Resolve the authenticated user.
2. Verify that the requested session belongs to that user.
3. Load messages and their citations without an N+1 query.
4. Order messages by creation time and citations by `CitationIndex`.
5. Return `citations: []` for messages without citations rather than `null`.

An absent session and a session owned by another user both return `404` to avoid disclosing session identifiers.

## API Contract

No endpoint is added. `ChatMessageResponseDto.Citations` becomes a non-null collection in history and create-message responses.

Each citation retains the existing public fields:

```json
{
  "documentId": "00000000-0000-0000-0000-000000000000",
  "source": "example.pdf",
  "snippet": "Exact cited text",
  "pageNumber": 12,
  "relevance": 0.92,
  "matchType": "hybrid",
  "isHighlightable": true,
  "reason": null
}
```

Array position is the citation marker: the first item resolves `[1]`, the second resolves `[2]`. `CitationIndex` remains an internal persistence detail unless the existing Frontend contract later requires it explicitly.

Frontend session-routing contract:

- Recommended route: `/ai-notebook/{sessionId}`.
- Reload messages with `GET /api/Chat/sessions/{sessionId}/messages`.
- Reload attached documents with `GET /api/Chat/sessions/{sessionId}/documents`.
- On citation click, open `documentId`, navigate to `pageNumber` when present, and highlight `snippet` only when `isHighlightable` is true.

## Migration and Compatibility

Create one EF Core migration for the citation table, relationship, and unique index. Existing chat messages receive no fabricated citation data, so their response contains an empty citation array. New messages persist citations from deployment onward.

The response is backward-compatible for clients that already accept nullable citations; the only behavioral change is that history responses now contain persisted data or an empty array.

## Error Handling

- Reject citation rows whose `DocumentId` is empty or whose required snapshot text is missing before persistence.
- Preserve nullable page numbers for formats without authoritative pages.
- Roll back assistant-message persistence if any citation row cannot be stored.
- Do not query Qdrant as a fallback during history loading.

## Testing

- Entity configuration enforces the foreign key, cascade delete, and unique ordered index.
- Creating an assistant message persists all citation fields and their order.
- Reading history through a fresh database context returns the stored citations.
- Messages without citations return an empty collection.
- Deleting a message or session cascades citation deletion.
- Deleting a document does not delete its historical citation snapshot.
- A user cannot load messages for another user's session.
- A citation persistence failure does not leave an assistant message with unresolved markers.
- EF migration applies successfully to the configured development database.

## Success Criteria

- Refreshing or reopening a chat session returns the same citation metadata originally returned with each assistant message.
- Citation marker ordering remains stable.
- History loading does not call the LLM, embedding service, or Qdrant.
- Existing chat and session endpoints remain the integration surface for the Frontend.
