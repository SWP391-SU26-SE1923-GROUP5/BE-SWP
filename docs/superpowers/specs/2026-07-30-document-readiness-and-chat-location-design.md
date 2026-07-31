# Document Readiness and Chat Location Design

**Date:** 2026-07-30

**Status:** Approved in conversation; awaiting written-spec review

## 1. Purpose

This design closes two backend gaps in the asynchronous document workflow:

1. A user can attach a document to Chat and send a message before document
   processing has produced searchable content.
2. After citations were removed, Chat no longer reports the document pages
   associated with an answer reliably, even though trusted `pageNumber`
   metadata remains in the RAG pipeline.

The public experience must use user-facing language such as "preparing" and
"ready". It must not expose implementation terms such as embedding, chunking,
vectors, OpenAI, or Qdrant.

## 2. Scope and Constraints

This change is backend-only and preserves the existing three-layer solution.

In scope:

- A reusable readiness decision for documents.
- Readiness information in Document and Chat-related responses.
- A Chat guard that rejects a message when any attached document is not ready.
- Friendly failure and retry information for the UI.
- SignalR notification payloads that do not expose technical failures.
- Deterministic document/page location text appended to relevant Chat answers.
- Frontend/API documentation and a manual verification handoff.

Out of scope:

- Database schema changes or migrations.
- Editing or deleting any existing migration.
- Changing the single-reader document-processing queue.
- Parallel document processing.
- Persisted processing stages or percentage progress.
- Reintroducing citation entities, DTOs, arrays, markers, snippets, or source
  sections.
- Creating or running unit, integration, end-to-end, or smoke tests.

The worker continues to process one document at a time. Multiple uploads may be
accepted concurrently, but their queued processing remains sequential.

## 3. Mandatory Pre-implementation Gate

Another member is changing the same branch but has not pushed those changes at
the time this design was written. No production implementation may begin from
the currently inspected code.

Before touching production code, the implementer must:

1. Wait for the member to push the pending changes.
2. Pull the latest branch state.
3. Record the pre-pull and post-pull commit IDs.
4. Review the incoming diff, especially changes to Document upload and
   processing, Chat, RAG orchestration, DTOs, global middleware, SignalR
   notifications, and active documentation.
5. Re-check every current-state assumption in this design against the updated
   code.
6. Update this design and its implementation plan if interfaces, behavior, or
   file ownership have changed.
7. Report the revised scope and possible conflicts to the repository owner.
8. Obtain explicit approval before implementation starts.

This gate is part of the feature acceptance criteria, not an optional workflow
note.

## 4. Current-State Findings

### 4.1 Document processing

`POST /api/Document/upload/file` durably saves the file and a `Processing`
Document, queues metadata, and returns `202 Accepted`. The background worker
then extracts content, chunks it, generates embeddings, and upserts every valid
chunk into Qdrant.

For PDF, DOCX, TXT, MD, and supported image/OCR files, the worker changes the
Document to `Done` only after vector upserts complete. A failed run deletes the
partial vectors associated with its `indexRunId`, marks the Document `Failed`,
retains the source file, and stores a technical error internally.

The worker already retries embedding failures up to three times before the run
fails. Application startup recovers active Documents left in `Processing`.

The active `DocumentResponseDto` contains `ErrorMessage`, so list/detail
responses can expose the technical pipeline error stored on the entity. That
conflicts with the approved user-facing error policy and must be corrected.

Audio and video extensions may be accepted and eventually marked `Done`, but
the current worker intentionally skips vectorization for them. Therefore
`Status == Done` alone is not a sufficient Chat-readiness rule.

### 4.2 Chat

`AIChatService.AddDocumentAsync` checks session and Document ownership but does
not check readiness. This permits a `Processing` or `Failed` Document to be
attached, which is an acceptable UX decision for this design.

`AIChatService.CreateMessageAsync` currently sends all attached Document IDs to
RAG without first checking their processing state. If vectors are missing or
incomplete, the Chat request can produce an irrelevant or missing-context
answer rather than a controlled readiness error.

### 4.3 Page metadata

The ingestion worker stores a positive physical page number in Qdrant metadata
when extraction can determine one. `RagContextSelector` parses and validates
that metadata, and `RagPromptContextBuilder` provides it to the model.

The current prompt merely instructs the model to mention a page in limited
circumstances. Model compliance is not deterministic, so page reporting can be
omitted. Page presentation must therefore be performed programmatically after
retrieval rather than delegated to the model.

## 5. Architecture

### 5.1 Document readiness evaluator

Introduce one Business-layer readiness component used by all relevant service
paths. It translates internal Document state into a stable, user-facing
decision without querying Qdrant.

Conceptual result:

```text
DocumentReadiness
|- Status
|- IsChatReady
|- Message
`- CanRetry
```

The evaluator owns the supported RAG extension set so the worker, reindex
policy, readiness checks, and future callers do not develop contradictory
extension rules. At minimum it covers the currently vectorized extensions:

```text
.pdf .docx .txt .md .jpg .jpeg .png .webp .gif
```

Readiness rules:

| Document state | `isChatReady` | `canRetry` | User-facing result |
|---|---:|---:|---|
| Active, supported, `Processing` | false | false | Tài liệu đang được chuẩn bị. |
| Active, supported, `Done` | true | false | Tài liệu đã sẵn sàng. |
| Active, supported, `Failed` | false | true | Không thể chuẩn bị tài liệu. |
| Active, unsupported media, `Done` | false | false | Loại tài liệu này không hỗ trợ Chat. |
| Draft, Archived, Banned, Trashed, or Purged | false | false | Tài liệu không khả dụng cho Chat. |

`canRetry` means the retry action is valid for the state. The reprocess service
remains authoritative for source-file existence and other runtime validation.

The evaluator must not require the latest `ProcessingVersion` for Chat. A
failed background reindex deliberately preserves an older usable index and
leaves the Document `Done`; blocking it would defeat that availability rule.

### 5.2 RAG location formatter

Introduce a pure Business-layer formatter that receives the selected
`RagContextSource` values and produces user-facing location text. It operates
only on metadata already admitted by `RagContextSelector`.

Responsibilities:

- Group contexts by `documentId`, not only by file name.
- Use the source/file name as the display label.
- Accept only positive `pageNumber` values.
- Remove duplicates and sort pages in ascending order.
- Compress only consecutive pages into ranges.
- Keep gaps explicit: pages `2, 3, 8` become `trang 2-3 và trang 8`.
- Never infer a page from `chunkIndex`, retrieval position, document length, or
  model output.
- State `không xác định được trang` for a selected document whose contexts do
  not contain any trusted page.
- When one selected document has both known and unknown-page contexts, list the
  known pages and add `một số đoạn không xác định được trang` for that same
  document.

Example:

```text
Vị trí nội dung liên quan trong tài liệu:
- Giáo trình A.pdf: trang 2-3 và trang 8
- Bài tập.docx: không xác định được trang
```

This is plain answer text, not a citation contract.

## 6. Public API Contracts

### 6.1 Shared readiness fields

Relevant Document responses expose:

```json
{
  "status": "Processing",
  "isChatReady": false,
  "message": "Tài liệu đang được chuẩn bị.",
  "canRetry": false
}
```

The fields are added to:

- The upload and reprocess response.
- `GET /api/Document/{id}/status`.
- Document list and detail responses.
- Documents returned for a Chat session.
- Documents listed in a Chat readiness error.

Existing response fields that consumers may use remain intact unless the
post-pull audit proves that the member's pending work has deliberately changed
their contract.

One existing field is deliberately excluded from that compatibility promise:
raw `ErrorMessage` must no longer be returned by public Document DTOs. The new
friendly `message` field replaces it for UI decisions. The entity field remains
available internally for diagnosis.

New readiness-specific and conflict responses serialize `status` as a stable
status name such as `Processing`, `Done`, or `Failed`. During the post-pull
audit, the implementation must inspect how existing Document list/detail DTOs
serialize their enum. It must either preserve that legacy field and add the
readiness fields without ambiguity, or explicitly document a coordinated
frontend contract change; it must not silently change numeric enum JSON into a
string.

`isChatReady` is the frontend's authoritative decision. The frontend must not
duplicate extension or lifecycle rules.

### 6.2 Upload and polling

Upload continues to return `202 Accepted` immediately after durable acceptance:

```json
{
  "documentId": "00000000-0000-0000-0000-000000000000",
  "status": "Processing",
  "isChatReady": false,
  "message": "Tài liệu đang được chuẩn bị.",
  "canRetry": false
}
```

The frontend uses SignalR for prompt updates and status polling as a fallback.
Each document completion or failure notification mirrors the readiness
evaluation of the persisted Document, including `status`, `isChatReady`,
`message`, and `canRetry`. Therefore, accepted unsupported media that completes
without indexing reports `Done` with `isChatReady=false` and the unsupported
message; it must not use ready title/body copy. On receiving a notification, the
frontend refetches the authoritative API state. Polling stops when readiness
reaches a terminal state: ready, failed, or unavailable.

The backend does not expose a percentage because it does not persist reliable
stage progress. The UI should show an indeterminate preparing state rather than
a fabricated percentage.

### 6.3 Chat attachment

`POST /api/Chat/sessions/{sessionId}/documents` continues to permit attaching a
Document while it is `Processing` or `Failed`. Its response includes readiness
fields so the UI can display the item and disable message submission.

This allows a user to upload, enter a Chat session immediately, and wait there
without repeatedly selecting the Document.

### 6.4 Chat message readiness conflict

Before persisting a user message or calling AI, `CreateMessageAsync` loads all
Documents attached to the owned session and evaluates each one.

If any attachment is not Chat-ready, the entire message is rejected. The
backend must not silently omit unready Documents or answer using a partial set.

The Business layer raises a dedicated readiness exception containing all
blocking Documents. Global exception middleware maps it to `409 Conflict`:

```json
{
  "statusCode": 409,
  "code": "DOCUMENTS_NOT_READY",
  "message": "Một hoặc nhiều tài liệu chưa sẵn sàng.",
  "documents": [
    {
      "documentId": "00000000-0000-0000-0000-000000000001",
      "title": "Bài giảng.pdf",
      "status": "Processing",
      "isChatReady": false,
      "message": "Tài liệu đang được chuẩn bị.",
      "canRetry": false
    }
  ]
}
```

The guard runs before:

- Adding the user `ChatMessage`.
- Invoking RAG or the LLM.
- Recording AI token usage.

When a session has no attached Document, the current friendly response asking
the user to attach one remains unchanged.

## 7. Processing Failure and Retry

The existing retry and cleanup behavior remains the foundation:

1. Retry transient embedding failure internally up to three attempts.
2. If processing still fails, remove vectors belonging to the incomplete
   `indexRunId`.
3. Keep the source file and Document record.
4. Set the Document to `Failed`.
5. Store and log technical details for backend diagnosis.
6. Notify the frontend using user-facing language only.

The failure notification must not contain raw exception text, provider names,
connection details, stack traces, document content, or Qdrant/OpenAI terms.
Technical details remain in structured backend logs and internal fields.

The UI displays:

```text
Không thể chuẩn bị tài liệu.
[Thử lại] [Xóa tài liệu]
```

The retry action calls:

```text
POST /api/Document/{documentId}/reprocess
```

After `202 Accepted`, readiness becomes `Processing`, `canRetry` becomes false,
and the UI disables repeated submissions while resuming SignalR plus polling.
The ordinary UI exposes retry only for `Failed`, although the existing endpoint
may continue to support an owner-requested reprocess of a `Done` Document.

The existing queue deduplication prevents simultaneous work for the same
Document. No new automatic whole-document retry loop is introduced beyond the
pipeline's existing bounded transient retries.

A failed reindex of an already usable legacy Document remains a special case:
the new partial run is removed, the old vectors remain, and readiness remains
true.

## 8. Deterministic Page Location in Chat

Location formatting uses the exact contexts selected for the current RAG
request. The model produces only the semantic answer; the backend then appends
the deterministic location summary.

Append the summary when:

- RAG selected one or more valid document contexts.
- The answer is marked relevant.
- The response represents an answer grounded in those contexts, including a
  deterministic yes/no shortcut that used retrieved content.

Do not append it when:

- No valid context was found.
- Relevance checks conclude that the document does not contain the topic.
- The response is a system-level error or readiness conflict.
- No Document is attached.

The wording is intentionally "Vị trí nội dung liên quan trong tài liệu" rather
than claiming that every generated sentence maps exactly to every listed page.
The selected retrieval contexts indicate where related supporting content was
found; they do not provide claim-level citation provenance.

The final answer text, including its location section, is persisted as the
assistant `ChatMessage`. A later history read therefore reproduces exactly what
the user originally received.

The internal RAG result may carry a location summary or selected-context
metadata as needed between the orchestrator and Chat service. It must not expose
a public citation/source array or recreate citation persistence.

## 9. SignalR Behavior

The existing `ReceiveNotification` event remains the transport.

Success communicates that the Document is ready. Failure communicates only
that the Document could not be prepared. Both payloads identify the Document so
the frontend can refetch its state.

The failure payload must not expose the internal `ErrorMessage`. If the current
payload type contains raw error text, the implementation replaces it with a
safe readiness-oriented shape after checking the post-pull contract.

SignalR is an acceleration mechanism, not the source of truth. The frontend
must refetch readiness after an event and use polling after reconnects or when
no event arrives.

## 10. Compatibility and Risk

Expected contract changes are additive readiness fields. The Chat response
shape remains unchanged; only `content` gains the location section. Citation
fields remain absent.

Intentional compatibility changes:

- A frontend that consumed raw technical text from the document-failure
  SignalR payload must switch to the friendly readiness message.
- Public Document responses no longer expose the entity's raw `ErrorMessage`;
  clients use the friendly readiness `message` instead.

Primary implementation risks:

- Duplicating supported-extension lists across workers and readiness checks.
  Mitigation: establish one shared policy during the post-pull design audit.
- Persisting an attempted Chat message before readiness validation.
  Mitigation: enforce ordering explicitly and inspect the final service flow.
- Treating retrieval chunks as claim-level citations.
  Mitigation: use the "related content location" wording and keep locations out
  of citation contracts.
- Inferring false page ranges across gaps.
  Mitigation: compress consecutive values only.
- Member changes invalidating current file-level assumptions.
  Mitigation: the mandatory post-pull audit and approval gate.

## 11. Verification Policy and Manual Handoff

Per repository policy, implementation creates no test project or test file and
runs no unit, integration, end-to-end, or smoke test. Compilation may be
verified with `dotnet build AIStudyHub.slnx --no-restore`. Functional validation
is performed manually by the repository owner.

The eventual handoff must cover at least:

1. Upload one supported Document and observe `Processing` followed by ready.
2. Attach a `Processing` Document, attempt Chat, receive 409, and confirm that
   no user message or token usage was persisted.
3. Attach two Documents with only one unready; confirm the complete Chat request
   is blocked and the response identifies the correct blocker.
4. Confirm Chat succeeds after every attachment becomes ready.
5. Force processing failure and confirm only a friendly message reaches the UI.
6. Retry a failed Document, prevent duplicate clicks, and observe the new
   `Processing` lifecycle.
7. Disconnect SignalR and confirm polling still reaches the terminal state.
8. Confirm accepted audio/video media is not reported as Chat-ready merely
   because its processing status is `Done`.
9. Confirm consecutive pages are compressed and page gaps remain explicit.
10. Confirm locations from multiple Documents are grouped independently.
11. Confirm a selected DOCX/TXT context without trustworthy page metadata says
    that the page cannot be determined.
12. Confirm a document with mixed known/unknown-page contexts lists its trusted
    pages and states that some related passages have no determined page.
13. Confirm irrelevant/no-context answers contain no location section.
14. Confirm no API response contains citation arrays, source markers, snippets,
    stack traces, provider errors, or technical processing terms.
15. Confirm a failed background reindex leaves an older usable Document ready.

## 12. Acceptance Criteria

- No database migration or schema edit is created.
- Existing migrations and the model snapshot are untouched.
- The queue remains single-reader and processes one Document at a time.
- UI-facing readiness is available without querying Qdrant.
- Processing and Failed Documents may remain attached to a Chat session.
- A Chat message is rejected when any attachment is not ready.
- The 409 payload lists every blocking Document with friendly readiness data.
- Rejected Chat attempts do not persist messages or consume AI tokens.
- Processing failures preserve the source file and support manual retry.
- Technical processing failures are not exposed through REST or SignalR.
- Every relevant document-grounded answer includes deterministic per-document
  location information.
- Positive pages are grouped correctly; wholly and partially unknown page
  metadata is stated explicitly; `chunkIndex` is never treated as a page.
- Citation persistence and public citation contracts remain removed.
- The latest member changes are pulled and audited, and the repository owner
  explicitly approves the revised plan before any implementation begins.
