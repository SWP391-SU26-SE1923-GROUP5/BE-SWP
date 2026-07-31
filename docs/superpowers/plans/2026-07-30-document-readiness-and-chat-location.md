# Document Readiness and Chat Location Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prevent Chat from using documents before searchable content is ready, expose friendly retry/readiness state to the UI, and append deterministic per-document page locations to relevant answers without restoring citations.

**Architecture:** Add a pure Business-layer RAG file policy and readiness evaluator, then reuse them in Document, SignalR, and Chat contracts. Chat validates every attached document before persisting a message or invoking AI. A separate pure formatter derives page labels from already-selected RAG contexts and appends plain text to grounded answers; no source metadata is added to public DTOs or the database.

**Tech Stack:** ASP.NET Core 8 Web API, C# 12, Entity Framework Core 8, AutoMapper, SignalR, OpenAI-backed RAG, Qdrant, PowerShell, Git.

## Global Constraints

- This plan is documentation only until the repository owner confirms the other member has pushed and the latest branch has been pulled.
- Task 0 is a mandatory stop-and-review gate. Do not start Task 1 without renewed owner approval.
- Keep exactly the existing `AIStudyHub.API`, `AIStudyHub.Business`, and `AIStudyHub.Data` layers.
- Do not add a project, package, database column, table, migration, or manual model-snapshot edit.
- Never edit, rename, move, regenerate, squash, or delete an existing migration.
- Do not recreate `AIStudyHub.Tests` or create/run unit, integration, end-to-end, or smoke tests.
- Verification commands are limited to source audits, `git diff --check`, and `dotnet build AIStudyHub.slnx --no-restore`.
- Functional verification is a manual handoff for the repository owner; the implementing agent does not execute it as a smoke test.
- Keep `DocumentProcessingQueue` single-reader; do not add parallel document processing.
- UI copy must not expose embedding, chunking, vector, OpenAI, Qdrant, stack traces, or raw provider errors.
- Do not recreate citation entities, tables, DTOs, arrays, snippets, source markers, or highlight metadata.
- Keep page reporting as plain assistant-message text derived only from positive `pageNumber` metadata; never use `chunkIndex` as a page.
- Commit each independently reviewable implementation task. Do not push until the repository owner explicitly requests it.

---

## File Responsibility Map

**Create:**

- `AIStudyHub.Business/AI/DocumentRagFilePolicy.cs` — one authoritative classification for RAG-supported text and image extensions.
- `AIStudyHub.Business/DTOs/Documents/DocumentReadinessDtos.cs` — shared readiness and blocking-document response records.
- `AIStudyHub.Business/Services/DocumentReadinessEvaluator.cs` — maps a Document to friendly readiness state without I/O.
- `AIStudyHub.Business/Exceptions/DocumentsNotReadyException.cs` — carries every blocking Chat attachment to middleware.
- `AIStudyHub.Business/AI/Orchestration/RagLocationFormatter.cs` — groups trusted pages and appends the location section.

**Modify:**

- `AIStudyHub.Business/DTOs/Documents/DocumentDtos.cs` — remove raw technical errors and add readiness fields.
- `AIStudyHub.Business/DTOs/Rag/UploadDocumentResponseDto.cs` — expose upload/reprocess readiness.
- `AIStudyHub.Business/DTOs/AIChat/ChatDtos.cs` — expose attachment readiness.
- `AIStudyHub.Business/DTOs/Notifications/RealTimeNotificationDto.cs` — use safe document readiness payloads.
- `AIStudyHub.Business/Mappings/ApplicationMappingProfile.cs` — map Document readiness without exposing `ErrorMessage`.
- `AIStudyHub.Business/Services/ModuleServices.cs` — make every manual Document mapping use the evaluator.
- `AIStudyHub.Business/Services/DocumentUploadService.cs` — build readiness-aware 202 responses.
- `AIStudyHub.Business/Services/DocumentReindexPolicy.cs` — reuse the shared RAG extension policy.
- `AIStudyHub.Business/Workers/DocumentBackgroundProcessor.cs` — reuse shared extension classification and emit safe notifications.
- `AIStudyHub.Business/Interfaces/Services/IRealTimeNotificationService.cs` — remove raw failure text from the push contract.
- `AIStudyHub.Business/Services/RealTimeNotificationService.cs` — send Vietnamese user-facing readiness notifications.
- `AIStudyHub.API/Controllers/DocumentController.cs` — return a typed readiness status response.
- `AIStudyHub.Business/AI/Chat/AIChatService.cs` — validate all attachments before message persistence/AI and map attachment readiness.
- `AIStudyHub.API/Middleware/GlobalExceptionMiddleware.cs` — map the dedicated Chat readiness exception to structured 409 JSON.
- `AIStudyHub.Business/AI/Orchestration/SemanticKernelOrchestrator.cs` — append deterministic page-location text on relevant paths.
- `AGENT.md`, `ARCHITECTURE.md`, `README.md`, `docs/FRONTEND_GUIDE.md`, `docs/api-contract.md`, and `docs/backend-feature-status.md` — describe the final contract and remove stale claims.

---

### Task 0: Pull-Aware Audit and Owner Reapproval Gate

**Files:**

- Review: `docs/superpowers/specs/2026-07-30-document-readiness-and-chat-location-design.md`
- Review: `docs/superpowers/plans/2026-07-30-document-readiness-and-chat-location.md`
- Review all source files in the File Responsibility Map.
- Modify only the two docs above if incoming changes invalidate an assumption.

**Interfaces:**

- Consumes: The member's pushed changes and the repository owner's confirmation that the branch has been pulled.
- Produces: A written audit result, an updated spec/plan if required, and explicit renewed approval to begin implementation.

- [ ] **Step 1: Wait for the repository owner**

Do not run a pull or edit production code. Wait until the owner states that the member has pushed and the latest branch has been pulled.

- [ ] **Step 2: Record the updated repository state**

Run:

```powershell
git status --short
git branch --show-current
git rev-parse HEAD
git log -12 --oneline --decorate
```

Expected: the intended branch is checked out, the owner's/member's commits are visible, and unrelated worktree changes are identified before any edit.

- [ ] **Step 3: Identify incoming changes since this design**

Use the design commit as the lower bound:

```powershell
git log b43e431..HEAD --oneline --decorate
git diff b43e431..HEAD -- AIStudyHub.API/Controllers/DocumentController.cs AIStudyHub.API/Middleware/GlobalExceptionMiddleware.cs AIStudyHub.Business/AI/Chat/AIChatService.cs AIStudyHub.Business/AI/Orchestration AIStudyHub.Business/DTOs AIStudyHub.Business/Services/DocumentUploadService.cs AIStudyHub.Business/Services/RealTimeNotificationService.cs AIStudyHub.Business/Workers/DocumentBackgroundProcessor.cs
```

If history has been rebased and `b43e431` is not an ancestor, identify the merge base without rewriting history:

```powershell
git merge-base b43e431 HEAD
```

Then diff that merge-base against `HEAD` for the same paths.

- [ ] **Step 4: Re-run the structural audit**

Run:

```powershell
rg -n "GetUploadStatus|Reprocess\(|UploadDocumentResponseDto|DocumentStatus\.(Processing|Done|Failed)|ProcessingVersion" AIStudyHub.API AIStudyHub.Business AIStudyHub.Data -g "*.cs"
rg -n "AddDocumentAsync|CreateMessageAsync|ChatSessionDocumentResponseDto|ErrorMessage|NotifyDocumentFailedAsync" AIStudyHub.API AIStudyHub.Business -g "*.cs"
rg -n "pageNumber|PageNumber|RagContextSource|RagPromptContextBuilder|RagResponseWithUsage|citation|Citation" AIStudyHub.Business AIStudyHub.API -g "*.cs"
rg -n "DocumentProcessingQueue|SingleReader|DocumentBackgroundProcessor|AddHostedService" AIStudyHub.API AIStudyHub.Business -g "*.cs"
```

Confirm all of these assumptions or document the exact replacement behavior:

- `Done` is assigned after all vector upserts for supported documents.
- Partial current-run vectors are removed on failure.
- reindex failure keeps old usable vectors and `Done` status.
- Chat currently lacks or has gained an attachment-readiness guard.
- the public Document DTO currently exposes or has removed raw `ErrorMessage`.
- page metadata still reaches `RagContextSource`.
- the queue remains single-reader.
- citations remain absent from public Chat contracts and persistence.

- [ ] **Step 5: Update documentation if the audit changes the design**

Edit only the design and plan docs. Replace stale paths, signatures, or task steps with the new exact code shape. Do not leave alternative branches or unresolved notes in the plan.

Run:

```powershell
git diff --check
```

Expected: the documents contain complete decisions and no whitespace errors.

- [ ] **Step 6: Commit audit-only document corrections when needed**

If Step 5 changed either document:

```powershell
git add docs/superpowers/specs/2026-07-30-document-readiness-and-chat-location-design.md docs/superpowers/plans/2026-07-30-document-readiness-and-chat-location.md
git diff --cached --check
git commit -m "docs: align readiness plan with member changes"
```

If no correction is required, do not create an empty commit.

- [ ] **Step 7: Stop and request renewed approval**

Report:

- Current HEAD.
- Incoming commits reviewed.
- Files that overlap this plan.
- Assumptions that remained valid.
- Every spec/plan adjustment.
- Whether the working tree is clean.

Stop. Task 1 is forbidden until the repository owner explicitly approves implementation after this report.

---

### Task 1: Add the Shared RAG File and Readiness Policies

**Files:**

- Create: `AIStudyHub.Business/AI/DocumentRagFilePolicy.cs`
- Create: `AIStudyHub.Business/DTOs/Documents/DocumentReadinessDtos.cs`
- Create: `AIStudyHub.Business/Services/DocumentReadinessEvaluator.cs`
- Modify: `AIStudyHub.Business/DTOs/Documents/DocumentDtos.cs`
- Modify: `AIStudyHub.Business/Mappings/ApplicationMappingProfile.cs`
- Modify: `AIStudyHub.Business/Services/ModuleServices.cs`
- Modify: `AIStudyHub.Business/Services/DocumentReindexPolicy.cs`
- Modify: `AIStudyHub.Business/Workers/DocumentBackgroundProcessor.cs`

**Interfaces:**

- Produces: `DocumentRagFilePolicy.IsTextDocument`, `IsImageDocument`, and `SupportsChat`.
- Produces: `DocumentReadinessEvaluator.Evaluate(Document)` returning `DocumentReadinessDto`.
- Produces: readiness fields on `DocumentResponseDto` without public `ErrorMessage`.
- Consumes: existing `DocumentStatus`, `DocumentLifecycleStatus`, `Document.FileName`, and `Document.FileExtension`.

- [ ] **Step 1: Define one authoritative RAG file policy**

Create:

```csharp
namespace AIStudyHub.Business.AI;

public static class DocumentRagFilePolicy
{
    private static readonly HashSet<string> TextExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".docx", ".txt", ".md"
        };

    private static readonly HashSet<string> ImageExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif"
        };

    public static bool IsTextDocument(string? fileName, string? fileExtension = null) =>
        TextExtensions.Contains(GetExtension(fileName, fileExtension));

    public static bool IsImageDocument(string? fileName, string? fileExtension = null) =>
        ImageExtensions.Contains(GetExtension(fileName, fileExtension));

    public static bool SupportsChat(string? fileName, string? fileExtension = null) =>
        IsTextDocument(fileName, fileExtension)
        || IsImageDocument(fileName, fileExtension);

    private static string GetExtension(string? fileName, string? fileExtension)
    {
        var extension = !string.IsNullOrWhiteSpace(fileExtension)
            ? fileExtension
            : Path.GetExtension(fileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(extension))
            return string.Empty;

        return extension.StartsWith('.')
            ? extension.ToLowerInvariant()
            : $".{extension.ToLowerInvariant()}";
    }
}
```

- [ ] **Step 2: Define shared readiness response records**

Create:

```csharp
namespace AIStudyHub.Business.DTOs.Documents;

public sealed record DocumentReadinessDto(
    string Status,
    bool IsChatReady,
    string Message,
    bool CanRetry);

public sealed record DocumentReadinessStatusResponseDto(
    Guid Id,
    string Status,
    bool IsChatReady,
    string Message,
    bool CanRetry);

public sealed record BlockingDocumentResponseDto(
    Guid DocumentId,
    string Title,
    string Status,
    bool IsChatReady,
    string Message,
    bool CanRetry);
```

- [ ] **Step 3: Implement the pure readiness evaluator**

Create a static evaluator with this ordering:

```csharp
using AIStudyHub.Business.AI;
using AIStudyHub.Business.DTOs.Documents;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;

namespace AIStudyHub.Business.Services;

public static class DocumentReadinessEvaluator
{
    public static DocumentReadinessDto Evaluate(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var status = document.Status?.ToString() ?? "Unknown";

        if (document.LifecycleStatus != DocumentLifecycleStatus.Active
            || document.Status is DocumentStatus.Draft
                or DocumentStatus.Archived
                or DocumentStatus.Banned
                or DocumentStatus.Trashed)
        {
            return new(status, false, "Tài liệu không khả dụng cho Chat.", false);
        }

        if (!DocumentRagFilePolicy.SupportsChat(
                document.FileName,
                document.FileExtension))
        {
            return new(status, false, "Loại tài liệu này không hỗ trợ Chat.", false);
        }

        return document.Status switch
        {
            DocumentStatus.Done =>
                new(status, true, "Tài liệu đã sẵn sàng.", false),
            DocumentStatus.Processing =>
                new(status, false, "Tài liệu đang được chuẩn bị.", false),
            DocumentStatus.Failed =>
                new(status, false, "Không thể chuẩn bị tài liệu.", true),
            _ =>
                new(status, false, "Tài liệu chưa sẵn sàng.", false)
        };
    }
}
```

Do not query Qdrant, disk storage, or the database from this evaluator. Do not
check `ProcessingVersion`; a usable old index must stay available after a failed
reindex.

- [ ] **Step 4: Make public Document DTOs readiness-aware and safe**

In `DocumentResponseDto`, remove the public `ErrorMessage` positional property
and insert these properties immediately after the existing `Status`:

```csharp
bool IsChatReady,
string Message,
bool CanRetry,
```

Keep the existing `DocumentStatus? Status` property and its existing JSON
representation so this task does not silently change list/detail enum encoding.
The new status-specific DTO from Step 2 uses a string status.

- [ ] **Step 5: Update every Document mapping**

In `ApplicationMappingProfile`, replace the Document `ConstructUsing` expression
with a private mapper method so readiness is evaluated once:

```csharp
private static DocumentResponseDto MapDocument(Document source)
{
    var readiness = DocumentReadinessEvaluator.Evaluate(source);
    return new DocumentResponseDto(
        source.Id,
        source.UserId,
        source.SubjectId,
        source.Title,
        source.FileLink,
        source.FileName,
        source.FileExtension,
        source.FileType,
        source.FileSizeBytes,
        source.ShareStatus,
        source.Status,
        readiness.IsChatReady,
        readiness.Message,
        readiness.CanRetry,
        source.Votes != null ? source.Votes.Count : 0,
        source.LifecycleStatus,
        source.TrashedAt,
        source.CreatedAt,
        source.UpdatedAt);
}
```

Register it with:

```csharp
CreateMap<Document, DocumentResponseDto>()
    .ConvertUsing(source => MapDocument(source));
```

Remove `source.ErrorMessage` from the public DTO constructor.

In `DocumentService` inside `ModuleServices.cs`, convert `MapToDto` and
`MapToDtoNoVotes` from expression-bodied methods to block methods, evaluate the
Document once, and pass:

```csharp
readiness.IsChatReady,
readiness.Message,
readiness.CanRetry,
```

Search for every direct `new DocumentResponseDto` call and update it:

```powershell
rg -n "new DocumentResponseDto|DocumentResponseDto\(" AIStudyHub.API AIStudyHub.Business -g "*.cs"
```

Expected: no mapping passes `Document.ErrorMessage` into a response.

- [ ] **Step 6: Reuse the file policy in reindex and ingestion**

Replace `DocumentReindexPolicy`'s private extension set and
`IsSupportedFileName` implementation with `DocumentRagFilePolicy.SupportsChat`.

In `DocumentBackgroundProcessor`, replace the local text/image extension arrays
with:

```csharp
var isTextDocument = DocumentRagFilePolicy.IsTextDocument(
    request.FileName);
var isImageFile = DocumentRagFilePolicy.IsImageDocument(
    request.FileName);
```

Keep every processing branch, sequential behavior, retry, cleanup, and status
transition unchanged.

- [ ] **Step 7: Verify compilation and policy consistency**

Run:

```powershell
rg -n "src\.ErrorMessage|d\.ErrorMessage|SupportedExtensions|isTextDocument = new\[\]" AIStudyHub.API AIStudyHub.Business -g "*.cs"
dotnet build AIStudyHub.slnx --no-restore
git diff --check
```

Expected: no public mapping exposes `ErrorMessage`; old duplicated extension
sets used for readiness/reindex/worker classification are gone; build succeeds.

- [ ] **Step 8: Commit the shared policy slice**

```powershell
git add AIStudyHub.Business/AI/DocumentRagFilePolicy.cs AIStudyHub.Business/DTOs/Documents/DocumentReadinessDtos.cs AIStudyHub.Business/Services/DocumentReadinessEvaluator.cs AIStudyHub.Business/DTOs/Documents/DocumentDtos.cs AIStudyHub.Business/Mappings/ApplicationMappingProfile.cs AIStudyHub.Business/Services/ModuleServices.cs AIStudyHub.Business/Services/DocumentReindexPolicy.cs AIStudyHub.Business/Workers/DocumentBackgroundProcessor.cs
git diff --cached --check
git commit -m "refactor: expose safe document readiness"
```

---

### Task 2: Expose Readiness Through Upload, Status, and SignalR

**Files:**

- Modify: `AIStudyHub.Business/DTOs/Rag/UploadDocumentResponseDto.cs`
- Modify: `AIStudyHub.Business/Services/DocumentUploadService.cs`
- Modify: `AIStudyHub.API/Controllers/DocumentController.cs`
- Modify: `AIStudyHub.Business/DTOs/Notifications/RealTimeNotificationDto.cs`
- Modify: `AIStudyHub.Business/Interfaces/Services/IRealTimeNotificationService.cs`
- Modify: `AIStudyHub.Business/Services/RealTimeNotificationService.cs`
- Modify: `AIStudyHub.Business/Workers/DocumentBackgroundProcessor.cs`

**Interfaces:**

- Consumes: `DocumentReadinessEvaluator.Evaluate(Document)` from Task 1.
- Produces: readiness-aware upload/reprocess/status REST responses.
- Produces: safe `DocumentProcessedPayload` and `DocumentFailedPayload` values sent through `ReceiveNotification`.

- [ ] **Step 1: Extend the accepted-upload response without removing legacy fields**

Use this exact record:

```csharp
public sealed record UploadDocumentResponseDto(
    Guid DocumentId,
    string Status,
    int ChunkCount,
    string Message,
    bool IsChatReady,
    bool CanRetry);
```

`Message` is non-null because every readiness state has user-facing text.

- [ ] **Step 2: Build upload and reprocess responses from readiness**

After upload persistence and queue submission, evaluate the persisted Document
and return:

```csharp
var readiness = DocumentReadinessEvaluator.Evaluate(document);
return new UploadDocumentResponseDto(
    document.Id,
    readiness.Status,
    0,
    readiness.Message,
    readiness.IsChatReady,
    readiness.CanRetry);
```

Apply the same shape to `ReprocessAsync` after status has been committed as
`Processing`. Preserve file, quota, ownership, compensation, queue deduplication,
and 202 behavior.

- [ ] **Step 3: Return a typed status payload**

In `GetUploadStatus`, retain owner-only access and replace the anonymous object:

```csharp
var readiness = DocumentReadinessEvaluator.Evaluate(document);
return Ok(new DocumentReadinessStatusResponseDto(
    document.Id,
    readiness.Status,
    readiness.IsChatReady,
    readiness.Message,
    readiness.CanRetry));
```

Do not include `document.ErrorMessage`.

- [ ] **Step 4: Replace technical SignalR failure payloads**

Use readiness-oriented notification records:

```csharp
public sealed record DocumentProcessedPayload(
    Guid DocumentId,
    string Title,
    string Status,
    bool IsChatReady,
    string Message,
    bool CanRetry);

public sealed record DocumentFailedPayload(
    Guid DocumentId,
    string Title,
    string Status,
    bool IsChatReady,
    string Message,
    bool CanRetry);
```

Change both document notification methods to accept the evaluated readiness of
the persisted Document:

```csharp
Task NotifyDocumentProcessedAsync(
    Guid userId,
    Guid documentId,
    string title,
    DocumentReadinessDto readiness,
    CancellationToken cancellationToken = default);

Task NotifyDocumentFailedAsync(
    Guid userId,
    Guid documentId,
    string title,
    DocumentReadinessDto readiness,
    CancellationToken cancellationToken = default);
```

The worker already logs the caught exception. Remove `ex.Message` from its
notification call. After each success or failure state is committed, evaluate
that persisted Document and pass the resulting `DocumentReadinessDto` to the
notification service. Do not query Qdrant from the notification path.

- [ ] **Step 5: Send friendly notification content**

Supported, ready completion:

```text
Title: Tài liệu đã sẵn sàng
Body: "{title}" đã sẵn sàng để sử dụng.
Status: Done
IsChatReady: true
Message: Tài liệu đã sẵn sàng.
CanRetry: false
```

Retryable failure:

```text
Title: Không thể chuẩn bị tài liệu
Body: Không thể chuẩn bị "{title}". Vui lòng thử lại.
Status: Failed
IsChatReady: false
Message: Không thể chuẩn bị tài liệu.
CanRetry: true
```

Keep the transport method name `ReceiveNotification`, the user group, and
`NotificationType.Document` unchanged.

For all other results, including accepted unsupported media that persists as
`Done`, derive the notification title/body and every payload readiness field
from the passed evaluator result. Unsupported media must report `Done`,
`IsChatReady: false`, `CanRetry: false`, and
`Loại tài liệu này không hỗ trợ Chat.` without any ready claim. A failure
notification must likewise use the evaluator's actual readiness instead of
hardcoding `Failed` or `CanRetry: true`.

- [ ] **Step 6: Audit for technical error leakage and compile**

Run:

```powershell
rg -n "DocumentFailedPayload\(|NotifyDocumentFailedAsync\(|errorMessage|ex\.Message" AIStudyHub.API AIStudyHub.Business -g "*.cs"
dotnet build AIStudyHub.slnx --no-restore
git diff --check
```

Inspect every result. `ex.Message` may remain in structured backend logging and
the internal entity assignment, but not in REST/SignalR payload construction.

- [ ] **Step 7: Commit the public readiness slice**

```powershell
git add AIStudyHub.Business/DTOs/Rag/UploadDocumentResponseDto.cs AIStudyHub.Business/Services/DocumentUploadService.cs AIStudyHub.API/Controllers/DocumentController.cs AIStudyHub.Business/DTOs/Notifications/RealTimeNotificationDto.cs AIStudyHub.Business/Interfaces/Services/IRealTimeNotificationService.cs AIStudyHub.Business/Services/RealTimeNotificationService.cs AIStudyHub.Business/Workers/DocumentBackgroundProcessor.cs
git diff --cached --check
git commit -m "refactor: publish document readiness state"
```

---

### Task 3: Block Chat Messages Until Every Attachment Is Ready

**Files:**

- Create: `AIStudyHub.Business/Exceptions/DocumentsNotReadyException.cs`
- Modify: `AIStudyHub.Business/DTOs/AIChat/ChatDtos.cs`
- Modify: `AIStudyHub.Business/AI/Chat/AIChatService.cs`
- Modify: `AIStudyHub.API/Middleware/GlobalExceptionMiddleware.cs`

**Interfaces:**

- Consumes: `DocumentReadinessEvaluator.Evaluate(Document)` and `BlockingDocumentResponseDto` from Task 1.
- Produces: `DocumentsNotReadyException.Documents`.
- Produces: HTTP 409 with error code `DOCUMENTS_NOT_READY` and all blocking attachments.
- Produces: readiness fields on every `ChatSessionDocumentResponseDto`.

- [ ] **Step 1: Extend the attached-document DTO**

Use:

```csharp
public sealed record ChatSessionDocumentResponseDto(
    Guid ChatSessionId,
    Guid DocumentId,
    string Title,
    string? FileName,
    DateTime AddedAt,
    string Status,
    bool IsChatReady,
    string Message,
    bool CanRetry);
```

- [ ] **Step 2: Add the dedicated Business exception**

Create:

```csharp
using AIStudyHub.Business.DTOs.Documents;

namespace AIStudyHub.Business.Exceptions;

public sealed class DocumentsNotReadyException : Exception
{
    public DocumentsNotReadyException(
        IReadOnlyList<BlockingDocumentResponseDto> documents)
        : base("Một hoặc nhiều tài liệu chưa sẵn sàng.")
    {
        Documents = documents;
    }

    public IReadOnlyList<BlockingDocumentResponseDto> Documents { get; }
}
```

The exception contains friendly DTO data only, never entity `ErrorMessage`.

- [ ] **Step 3: Map attachment readiness consistently**

Add a private mapping helper in `AIChatService`:

```csharp
private static ChatSessionDocumentResponseDto MapSessionDocument(
    ChatSessionDocument link,
    Document document)
{
    var readiness = DocumentReadinessEvaluator.Evaluate(document);
    return new ChatSessionDocumentResponseDto(
        link.ChatSessionId,
        link.DocumentId,
        document.Title,
        document.FileName,
        link.CreatedAt,
        readiness.Status,
        readiness.IsChatReady,
        readiness.Message,
        readiness.CanRetry);
}
```

Use it for existing attachment returns, new attachment returns, and
`GetDocumentsAsync`. Continue to allow attaching Processing and Failed
Documents. Preserve session/document ownership checks and 404 behavior.

- [ ] **Step 4: Load and validate attachments before side effects**

For an existing session, load links with Documents once:

```csharp
var sessionDocumentLinks = await _unitOfWork.ChatSessionDocuments
    .Query()
    .Include(link => link.Document)
    .Where(link => link.ChatSessionId == session.Id)
    .AsNoTracking()
    .ToListAsync(ct);

var blockers = sessionDocumentLinks
    .Select(link => new
    {
        Link = link,
        Readiness = DocumentReadinessEvaluator.Evaluate(link.Document)
    })
    .Where(item => !item.Readiness.IsChatReady)
    .Select(item => new BlockingDocumentResponseDto(
        item.Link.DocumentId,
        item.Link.Document.Title,
        item.Readiness.Status,
        item.Readiness.IsChatReady,
        item.Readiness.Message,
        item.Readiness.CanRetry))
    .ToList();

if (blockers.Count > 0)
    throw new DocumentsNotReadyException(blockers);
```

Place this block after session ownership validation but before quota preflight,
user-message persistence, AI calls, or token recording. Reuse
`sessionDocumentLinks.Select(link => link.DocumentId)` for RAG so the service
does not issue a second attachment query.

For a newly created empty session, preserve the existing response asking the
user to attach a Document.

- [ ] **Step 5: Map the exception to an exact 409 body**

Add a dedicated catch before the generic `InvalidOperationException` catch:

```csharp
catch (DocumentsNotReadyException exception)
{
    context.Response.ContentType = "application/json";
    context.Response.StatusCode = StatusCodes.Status409Conflict;
    var payload = new
    {
        statusCode = context.Response.StatusCode,
        code = "DOCUMENTS_NOT_READY",
        message = exception.Message,
        documents = exception.Documents
    };
    await context.Response.WriteAsJsonAsync(payload);
}
```

Do not catch this exception in `ChatController`; global middleware owns the
contract. ASP.NET web JSON serialization is required so nested blocker fields
remain camelCase.

- [ ] **Step 6: Audit side-effect ordering and compile**

Read `CreateMessageAsync` from top to bottom and confirm the blocker check is
before:

```text
HasQuotaAsync
ChatMessages.AddAsync(userMessage)
AskWithTrackingAsync
RecordUsageAsync
```

Run:

```powershell
rg -n -C 8 "DocumentsNotReadyException|HasQuotaAsync|AddAsync\(userMessage|AskWithTrackingAsync|RecordUsageAsync" AIStudyHub.Business/AI/Chat/AIChatService.cs
dotnet build AIStudyHub.slnx --no-restore
git diff --check
```

Expected: build succeeds and the textual ordering confirms no rejected Chat
attempt produces a message or token side effect.

- [ ] **Step 7: Commit the Chat guard slice**

```powershell
git add AIStudyHub.Business/Exceptions/DocumentsNotReadyException.cs AIStudyHub.Business/DTOs/AIChat/ChatDtos.cs AIStudyHub.Business/AI/Chat/AIChatService.cs AIStudyHub.API/Middleware/GlobalExceptionMiddleware.cs
git diff --cached --check
git commit -m "fix: block chat until documents are ready"
```

---

### Task 4: Append Deterministic Page Locations Without Citations

**Files:**

- Create: `AIStudyHub.Business/AI/Orchestration/RagLocationFormatter.cs`
- Modify: `AIStudyHub.Business/AI/Orchestration/SemanticKernelOrchestrator.cs`

**Interfaces:**

- Consumes: `IReadOnlyList<RagContextSource>` already selected by `RagContextSelector`.
- Produces: `RagLocationFormatter.AppendToAnswer(string, IReadOnlyList<RagContextSource>)`.
- Preserves: existing `RagResponse`, `RagResponseWithUsage`, and public Chat DTO shapes.

- [ ] **Step 1: Implement page range compression**

Create `RagLocationFormatter` with a private method that turns sorted distinct
pages into ranges. The core loop must follow this shape:

```csharp
private static IReadOnlyList<(int Start, int End)> BuildRanges(
    IReadOnlyList<int> pages)
{
    if (pages.Count == 0)
        return [];

    var ranges = new List<(int Start, int End)>();
    var start = pages[0];
    var end = pages[0];

    foreach (var page in pages.Skip(1))
    {
        if (page == end + 1)
        {
            end = page;
            continue;
        }

        ranges.Add((start, end));
        start = page;
        end = page;
    }

    ranges.Add((start, end));
    return ranges;
}
```

Render a single page as `trang 8`, a range as `trang 2-3`, and join multiple
segments with ` và `.

- [ ] **Step 2: Group contexts by Document and retain unknown-page information**

Implement:

```csharp
public static string AppendToAnswer(
    string answer,
    IReadOnlyList<RagContextSource> contexts)
```

For each `DocumentId` group:

```csharp
var pages = group
    .Where(source => source.PageNumber is > 0)
    .Select(source => source.PageNumber!.Value)
    .Distinct()
    .OrderBy(page => page)
    .ToList();

var hasUnknownPage = group.Any(source => source.PageNumber is null);
var displayName = group
    .Select(source => source.Result.Source)
    .First(source => !string.IsNullOrWhiteSpace(source));
```

Use these exact cases:

```text
known only:       - File.pdf: trang 2-3 và trang 8
unknown only:     - File.docx: không xác định được trang
known + unknown:  - File.pdf: trang 2-3; một số đoạn không xác định được trang
```

Start the section with:

```text
Vị trí nội dung liên quan trong tài liệu:
```

Return `answer` unchanged if `contexts` is empty. Use `TrimEnd()` before adding
exactly two newline characters and the section. Do not emit document IDs,
chunk indexes, raw metadata labels, brackets, or JSON.

- [ ] **Step 3: Stop asking the model to format location text**

In `RagSystemPrompt`, replace the existing page-output instructions with one
rule:

```text
- Do not add document names, page numbers, source markers, or a source section; the backend appends trusted location information after generation.
```

Keep the source-only answering rules and all citation-removal constraints.

- [ ] **Step 4: Append locations on every relevant RAG return path**

In `AskAsync`, wrap only the final relevant generated answer:

```csharp
var answerWithLocation = RagLocationFormatter.AppendToAnswer(answer, contexts);
return new RagResponse(answerWithLocation, confidence, IsRelevant: true);
```

In `AskWithTrackingAsync`, apply the formatter to:

- The deterministic yes/no shortcut because it used retrieved contexts.
- The final model-generated relevant answer.

For the shortcut:

```csharp
var answerWithLocation =
    RagLocationFormatter.AppendToAnswer(noAnswer, contexts);
return new RagResponseWithUsage(
    answerWithLocation,
    1.0,
    0,
    0,
    IsRelevant: true);
```

Do not append locations to no-context, low-relevance, suggestion, or error
returns. Do not change `OrchestrationTypes.cs`; the formatted location remains
part of `Answer` so `AIChatService` persists and returns exactly the same text.

- [ ] **Step 5: Audit citation boundaries and compile**

Run:

```powershell
rg -n "RagLocationFormatter|IsRelevant: true|IsRelevant: false|pageNumber|chunkIndex|Citation|citation" AIStudyHub.Business/AI/Orchestration AIStudyHub.Business/DTOs/AIChat AIStudyHub.Data/Entities -g "*.cs"
dotnet build AIStudyHub.slnx --no-restore
git diff --check
```

Expected:

- Every relevant path with contexts uses the formatter.
- Every irrelevant/no-context path omits it.
- No citation type or public source array is added.
- `chunkIndex` is used only for chunk identity/order, never page display.
- Build succeeds.

- [ ] **Step 6: Commit the location slice**

```powershell
git add AIStudyHub.Business/AI/Orchestration/RagLocationFormatter.cs AIStudyHub.Business/AI/Orchestration/SemanticKernelOrchestrator.cs
git diff --cached --check
git commit -m "feat: append trusted page locations to chat"
```

---

### Task 5: Align Active Documentation and Prepare Manual Handoff

**Files:**

- Modify: `AGENT.md`
- Modify: `ARCHITECTURE.md`
- Modify: `README.md`
- Modify: `docs/FRONTEND_GUIDE.md`
- Modify: `docs/api-contract.md`
- Modify: `docs/backend-feature-status.md`

**Interfaces:**

- Consumes: final REST, SignalR, Chat, and page-location behavior from Tasks 1-4.
- Produces: one non-contradictory frontend/backend contract and the owner's manual verification checklist.

- [ ] **Step 1: Document readiness terminology and UI behavior**

Document these exact UI labels:

```text
Processing -> Tài liệu đang được chuẩn bị.
Done/ready -> Tài liệu đã sẵn sàng.
Failed -> Không thể chuẩn bị tài liệu.
Unsupported -> Loại tài liệu này không hỗ trợ Chat.
```

Document `isChatReady` as authoritative, SignalR as immediate notification,
status polling as fallback, and an indeterminate spinner rather than fake
percentage progress.

- [ ] **Step 2: Document the REST and SignalR contracts**

Add the exact upload/status readiness fields, the Chat attachment fields, the
`DOCUMENTS_NOT_READY` 409 response, and safe document notification payloads.

State clearly:

- Processing and Failed Documents may be attached.
- Message sending is blocked if any attachment is unready.
- Rejected sends persist no user message and consume no AI tokens.
- UI shows retry only when `canRetry=true`, disables it after click, and calls
  `POST /api/Document/{id}/reprocess`.
- REST and SignalR never expose internal `ErrorMessage`.

- [ ] **Step 3: Document automatic page-location text**

Replace the old active rule that pages are mentioned only for explicit location
questions. Document that every relevant grounded answer appends a plain-text
per-document location section, including known, unknown, and mixed-page cases.

Reaffirm that this is not a citation array or claim-level citation system.

- [ ] **Step 4: Audit stale active documentation**

Run:

```powershell
rg -n -i "embedding|chunking|qdrant|errorMessage|only when.*ask|explicit.*page|citation|Document processed|processing failed|isChatReady|canRetry|DOCUMENTS_NOT_READY" AGENT.md ARCHITECTURE.md README.md docs/FRONTEND_GUIDE.md docs/api-contract.md docs/backend-feature-status.md
```

Review every result in context. Technical architecture sections may name
embedding/Qdrant, but UI copy and public error examples may not. Citation-removal
rules must remain intact.

- [ ] **Step 5: Run final compilation and repository audits**

Run:

```powershell
dotnet build AIStudyHub.slnx --no-restore
git diff --check
git status --short
git diff --name-only b43e431..HEAD
rg --files | rg "AIStudyHub.Tests|Migrations/.+document.*readiness|Migrations/.+chat.*ready"
rg -n "ChatMessageCitation|CitationInfo|Citations\s*[,{(]|chunkIndex.*page|page.*chunkIndex" AIStudyHub.API AIStudyHub.Business AIStudyHub.Data -g "*.cs" -g "!AIStudyHub.Data/Migrations/**"
```

Expected:

- Build succeeds.
- No test project/file was added.
- No migration was added or modified.
- No active citation contract was restored.
- No code treats chunk order as a page number.
- The diff contains only approved source and documentation files.

- [ ] **Step 6: Commit documentation**

```powershell
git add AGENT.md ARCHITECTURE.md README.md docs/FRONTEND_GUIDE.md docs/api-contract.md docs/backend-feature-status.md
git diff --cached --check
git commit -m "docs: describe document readiness and chat locations"
```

- [ ] **Step 7: Hand off manual verification without executing it**

Give the repository owner this checklist:

1. Upload one supported Document and observe `Processing` followed by ready.
2. Attach a Processing Document, send a message, receive 409, and confirm no
   attempted user message appears after refreshing history.
3. Attach two Documents with only one unready; confirm the complete request is
   blocked and the response identifies only the correct blocker.
4. Confirm Chat succeeds after every attachment becomes ready.
5. Force processing failure and confirm UI receives only friendly text and
   displays retry.
6. Retry once, confirm the button disables, and observe Processing again.
7. Disconnect SignalR and confirm status polling still reaches a terminal state.
8. Confirm accepted audio/video media is never marked Chat-ready.
9. Ask a PDF question whose retrieved pages are consecutive; confirm a range.
10. Ask a question whose pages contain a gap; confirm separate segments.
11. Use multiple Documents; confirm locations are grouped by file.
12. Use a source with no trusted page; confirm `không xác định được trang`.
13. Use mixed known/unknown contexts for one file; confirm known pages plus the
    partial-unknown note.
14. Ask an irrelevant question; confirm no location section is appended.
15. Confirm Chat/history responses contain no citations array or source markers.
16. Cause a background reindex failure on an older usable Document and confirm
    Chat remains available through the old index.

Report build output, commit IDs, changed files, and these manual steps. Do not
claim functional success until the owner completes them.

---

## Completion Boundary

This plan is complete only when Tasks 1-5 have been implemented after Task 0's
renewed approval, compilation succeeds, every implementation slice is committed,
and the manual checklist has been handed to the repository owner. Pushing the
branch and performing manual functional verification are separate owner-directed
actions.
