# Upload Processing and Exact AI Generation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enforce an exact 5 MiB upload limit, remove duplicate whole-file buffering, recover in-memory processing after restart, and require exact user-selected Quiz/Flashcard counts.

**Architecture:** Move upload workflow from `DocumentController` into a focused Business service that accepts a Stream. Retain a lightweight in-memory single-reader queue, make enqueue non-blocking, and recover Documents still marked Processing at worker startup. Generation continues to use the existing AI services but adds consistent validation, ownership/readiness guards, bounded exact-count behavior, and explicit domain errors.

**Tech Stack:** ASP.NET Core 8 multipart forms, C# 12 Streams and Channels, FluentValidation 12, EF Core 8, OpenAI, Qdrant.

## Global Constraints

- Do not recreate `AIStudyHub.Tests` or create any test project/file.
- Do not run unit tests, integration tests, or smoke tests.
- Use `dotnet build AIStudyHub.slnx --no-restore` for agent verification.
- The repository owner performs all functional verification manually.
- Do not edit or delete existing migrations; this plan requires no new migration.
- Do not apply migrations, clear Qdrant, or delete uploaded data.
- Keep the exact file-content maximum at `5,242,880` bytes.
- Keep generation counts required and inclusive from 1 through 20.

---

## File Structure

### Upload boundary

- Create `AIStudyHub.Business/DTOs/Documents/DocumentUploadRequest.cs` for HTTP-neutral stream metadata.
- Create `AIStudyHub.Business/Exceptions/FileSizeLimitExceededException.cs`.
- Create `AIStudyHub.Business/Exceptions/StorageQuotaExceededException.cs`.
- Create `AIStudyHub.Business/Interfaces/Services/IDocumentUploadService.cs`.
- Create `AIStudyHub.Business/Services/DocumentUploadService.cs`.
- Modify `AIStudyHub.API/Controllers/DocumentController.cs` to delegate upload/reprocess.
- Modify `AIStudyHub.API/Middleware/GlobalExceptionMiddleware.cs` to map upload errors.
- Modify `AIStudyHub.Business/Services/BusinessServiceExtensions.cs` to register the service.
- Modify `AIStudyHub.Business/Interfaces/Services/IFileStorageService.cs` and `AIStudyHub.Business/Services/LocalFileStorageService.cs` only for path resolution/removal of the byte-array upload path.

### Configuration and queue

- Modify `AIStudyHub.Business/Options/DocumentStorageOptions.cs`.
- Modify `AIStudyHub.Business/Options/RagOptions.cs`.
- Modify `AIStudyHub.API/appsettings.json`.
- Modify `AIStudyHub.API/Program.cs` for a 6 MiB multipart body limit.
- Modify `AIStudyHub.Business/Services/DocumentProcessingQueue.cs`.
- Modify `AIStudyHub.Business/Workers/DocumentBackgroundProcessor.cs`.
- Modify `AIStudyHub.Business/Workers/DocumentReindexWorker.cs`.
- Modify `AIStudyHub.API/Controllers/AdminController.cs` for the changed queue contract.

### Exact generation

- Modify `AIStudyHub.Business/DTOs/Quizzes/AiGenerationDtos.cs`.
- Modify `AIStudyHub.Business/DTOs/Flashcards/FlashcardDtos.cs`.
- Modify `AIStudyHub.Business/Validators/Quizzes/QuizValidators.cs`.
- Modify `AIStudyHub.Business/Validators/Flashcards/FlashcardValidators.cs`.
- Create `AIStudyHub.Business/Exceptions/ExactGenerationCountException.cs`.
- Modify `AIStudyHub.API/Middleware/GlobalExceptionMiddleware.cs`.
- Modify `AIStudyHub.API/Controllers/AIController.cs`.
- Modify `AIStudyHub.Business/AI/Generators/QuizAiService.cs`.
- Modify `AIStudyHub.Business/AI/Generators/FlashcardAiService.cs`.

### Documentation

- Modify `README.md`, `AGENT.md`, `ARCHITECTURE.md`, and `docs/FRONTEND_GUIDE.md`.

---

### Task 1: Establish One Exact Upload Limit

**Files:**
- Modify: `AIStudyHub.Business/Options/DocumentStorageOptions.cs`
- Modify: `AIStudyHub.Business/Options/RagOptions.cs`
- Modify: `AIStudyHub.API/appsettings.json`
- Modify: `AIStudyHub.API/Program.cs`

**Interfaces:**
- Produces: `DocumentStorageOptions.MaxFileSizeBytes = 5_242_880`
- Produces: multipart body maximum `6 * 1024 * 1024`
- Removes: `RagOptions.MaxFileSizeBytes`

- [ ] **Step 1: Make storage own the limit**

Set:

```csharp
public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;
```

Remove `MaxFileSizeBytes` from `RagOptions`.

- [ ] **Step 2: Update configuration**

Set:

```json
"DocumentStorage": {
  "BasePath": "wwwroot/uploads/documents",
  "MaxFileSizeBytes": 5242880
}
```

Do not expose or copy unrelated secrets while editing `appsettings.json`.

- [ ] **Step 3: Allow multipart overhead**

In `Program.cs`, before controller registration completes:

```csharp
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 6L * 1024 * 1024;
});
```

Add `using Microsoft.AspNetCore.Http.Features;`.

- [ ] **Step 4: Verify one source remains and commit**

```powershell
rg -n "MaxFileSizeBytes" AIStudyHub.API AIStudyHub.Business -g "*.cs" -g "*.json"
dotnet build AIStudyHub.slnx --no-restore
git add AIStudyHub.Business/Options/DocumentStorageOptions.cs AIStudyHub.Business/Options/RagOptions.cs AIStudyHub.API/appsettings.json AIStudyHub.API/Program.cs
git diff --cached --check
git commit -m "fix: enforce five MiB upload limit"
```

Expected: runtime references point only to `DocumentStorageOptions`.

---

### Task 2: Add a Stream-Based Upload Service

**Files:**
- Create: `AIStudyHub.Business/DTOs/Documents/DocumentUploadRequest.cs`
- Create: `AIStudyHub.Business/Interfaces/Services/IDocumentUploadService.cs`
- Create: `AIStudyHub.Business/Services/DocumentUploadService.cs`
- Modify: `AIStudyHub.Business/Interfaces/Services/IFileStorageService.cs`
- Modify: `AIStudyHub.Business/Services/LocalFileStorageService.cs`
- Modify: `AIStudyHub.Business/Services/BusinessServiceExtensions.cs`

**Interfaces:**
- Produces:

```csharp
public sealed record DocumentUploadRequest(
    Guid UserId,
    Guid SubjectId,
    string Title,
    string FileName,
    string ContentType,
    long ContentLength,
    Stream Content);

public interface IDocumentUploadService
{
    Task<UploadDocumentResponseDto> UploadAsync(
        DocumentUploadRequest request,
        CancellationToken cancellationToken = default);

    Task<UploadDocumentResponseDto> ReprocessAsync(
        Guid documentId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
```

- Extends file storage with:

```csharp
public sealed record StoredFileResult(string RelativePath, long SizeBytes);

Task<StoredFileResult> SaveFileAsync(
    Stream fileStream,
    string fileName,
    string extension,
    long maxFileSizeBytes,
    CancellationToken cancellationToken = default);

string ResolveFullPath(string relativePath);
```

- [ ] **Step 1: Create the upload request and service contract**

Add the exact types above. The request owns no HTTP type and must not dispose the Stream; the API request scope owns it.

- [ ] **Step 2: Make file path resolution storage-owned**

Add to `LocalFileStorageService`:

```csharp
public string ResolveFullPath(string relativePath)
{
    var fullPath = Path.GetFullPath(Path.Combine(_baseDirectory, relativePath));
    var relativeToBase = Path.GetRelativePath(_baseDirectory, fullPath);
    if (Path.IsPathRooted(relativeToBase)
        || relativeToBase == ".."
        || relativeToBase.StartsWith(
            $"..{Path.DirectorySeparatorChar}",
            StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Resolved file path is outside document storage.");
    }

    return fullPath;
}
```

Remove the byte-array `SaveFileAsync` overload from the interface and implementation
after all call sites use Streams. Make `DeleteFileAsync` call `ResolveFullPath` rather
than combining an untrusted relative path directly.

- [ ] **Step 3: Enforce the limit while copying**

Implement the Stream overload with a reusable buffer and a running `long` byte count.
Read at most the configured maximum plus one byte. When the count exceeds
`maxFileSizeBytes`:

- close the destination stream;
- delete the partial file;
- throw `FileSizeLimitExceededException(actualBytes, maxFileSizeBytes)`.

Return `StoredFileResult(relativePath, actualBytes)` on success. Use the returned
byte count for quota calculation and `Document.FileSizeBytes`; never persist the
multipart `ContentLength` as the authoritative size.

- [ ] **Step 4: Implement validation in `DocumentUploadService`**

Validate in this order:

```csharp
if (request.UserId == Guid.Empty)
    throw new UnauthorizedAccessException("Authentication is required.");
if (request.ContentLength <= 0)
    throw new ValidationException("A non-empty file is required.");
if (string.IsNullOrWhiteSpace(request.Title))
    throw new ValidationException("Document title is required.");
if (request.SubjectId == Guid.Empty)
    throw new ValidationException("Subject id is required.");
if (request.ContentLength > _storageOptions.MaxFileSizeBytes)
    throw new FileSizeLimitExceededException(
        request.ContentLength,
        _storageOptions.MaxFileSizeBytes);
```

Create `FileSizeLimitExceededException` with `ActualBytes` and `LimitBytes`
properties. Map it to HTTP 413 with error code `FileSizeLimitExceeded` in the
global middleware.

Query the Subject with:

```csharp
subject.Id == request.SubjectId
    && subject.OwnerUserId == request.UserId
```

Missing and foreign Subjects throw `KeyNotFoundException("Subject not found.")`.

- [ ] **Step 5: Validate extension and byte-based quota**

Normalize:

```csharp
var safeFileName = Path.GetFileName(request.FileName);
var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
```

Reject unsupported extensions with `ValidationException`.

Before streaming, use the declared `ContentLength` for an early quota rejection.
After streaming, recompute with `StoredFileResult.SizeBytes` so the final decision
and persisted usage are byte-accurate:

Load the User and its Tier before computing quota. A missing User after JWT
validation returns 401. For a missing Tier, throw
`InvalidDataException("User tier is not configured.")` so it surfaces as a
server error, not as storage-quota exhaustion.

```csharp
var activeBytes = await _unitOfWork.Documents.Query()
    .Where(document => document.UserId == request.UserId
        && document.LifecycleStatus != DocumentLifecycleStatus.Purged)
    .SumAsync(document => (long?)document.FileSizeBytes, cancellationToken)
    ?? 0L;

var tierLimitBytes = (long)tier.StorageLimitMb * 1024 * 1024;
```

Create:

```csharp
public sealed class StorageQuotaExceededException : Exception
{
    public long CurrentBytes { get; }
    public long LimitBytes { get; }
    public long RequestedBytes { get; }
}
```

Map it to HTTP 403 with error code `StorageQuotaExceeded` and byte-valued response
fields. Do not reuse the AI-token-specific `QuotaExceededException`.

- [ ] **Step 6: Save stream, persist, compensate, and enqueue**

Core ordering:

```csharp
StoredFileResult? storedFile = null;
var documentWasCommitted = false;
try
{
    storedFile = await _fileStorage.SaveFileAsync(
        request.Content,
        Path.GetFileNameWithoutExtension(safeFileName),
        extension,
        _storageOptions.MaxFileSizeBytes,
        cancellationToken);

    // Create Document(Status=Processing), allocate an available file name
    // with the existing bounded unique-conflict retry,
    // persist storedFile.SizeBytes,
    // set CurrentStorageCapacity =
    //   (int)Math.Ceiling(newActiveBytes / (1024d * 1024d)),
    // and commit the Document/accounting together.

    documentWasCommitted = true;
    _processingQueue.TryEnqueue(processRequest);
    return new UploadDocumentResponseDto(
        document.Id,
        "processing",
        0,
        "Document is being processed in the background");
}
catch
{
    if (storedFile is not null && !documentWasCommitted)
        await _fileStorage.DeleteFileAsync(
            storedFile.RelativePath,
            CancellationToken.None);
    throw;
}
```

If final quota validation fails after streaming, compensation deletes the stored
file before throwing. Use `CancellationToken.None` for cleanup so client
cancellation does not strand a partial/orphaned file. A post-commit enqueue
failure must not delete the file or Document.

If all existing filename-allocation retries hit the active-name unique index,
throw `InvalidOperationException` so middleware returns 409 and the pre-commit
compensation removes the stored file.

- [ ] **Step 7: Implement reprocess through the same boundary**

`ReprocessAsync` must:

- Query by Document ID and owner User ID.
- Return 404 for missing/foreign Documents.
- Resolve the stored file safely.
- Return 400 when no stored source exists.
- Delete prior vectors.
- Set Document status Processing and clear ErrorMessage.
- Commit before `TryEnqueue`.
- Return 202-shaped DTO.

- [ ] **Step 8: Register, build, and commit**

Register:

```csharp
services.AddScoped<IDocumentUploadService, DocumentUploadService>();
```

Run:

```powershell
dotnet build AIStudyHub.slnx --no-restore
git add AIStudyHub.Business/DTOs/Documents/DocumentUploadRequest.cs AIStudyHub.Business/Interfaces/Services/IDocumentUploadService.cs AIStudyHub.Business/Interfaces/Services/IFileStorageService.cs AIStudyHub.Business/Services/DocumentUploadService.cs AIStudyHub.Business/Services/LocalFileStorageService.cs AIStudyHub.Business/Services/BusinessServiceExtensions.cs AIStudyHub.Business/Exceptions/FileSizeLimitExceededException.cs AIStudyHub.Business/Exceptions/StorageQuotaExceededException.cs AIStudyHub.API/Middleware/GlobalExceptionMiddleware.cs
git diff --cached --check
git commit -m "refactor: stream document uploads through business service"
```

---

### Task 3: Make the Queue Non-Blocking and Recoverable

**Files:**
- Modify: `AIStudyHub.Business/Services/DocumentProcessingQueue.cs`
- Modify: `AIStudyHub.Business/Workers/DocumentBackgroundProcessor.cs`
- Modify: `AIStudyHub.Business/Workers/DocumentReindexWorker.cs`
- Modify: `AIStudyHub.API/Controllers/AdminController.cs`

**Interfaces:**
- Produces:

```csharp
public interface IDocumentProcessingQueue
{
    bool TryEnqueue(DocumentProcessRequest request);
    IAsyncEnumerable<DocumentProcessRequest> DequeueAsync(
        CancellationToken cancellationToken = default);
    void Complete(Guid documentId);
}
```

- [ ] **Step 1: Replace the bounded queue**

Use:

```csharp
private readonly Channel<DocumentProcessRequest> _channel =
    Channel.CreateUnbounded<DocumentProcessRequest>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

private readonly ConcurrentDictionary<Guid, byte> _queuedDocumentIds = new();
```

Implement:

```csharp
public bool TryEnqueue(DocumentProcessRequest request)
{
    if (!_queuedDocumentIds.TryAdd(request.DocumentId, 0))
        return false;

    if (_channel.Writer.TryWrite(request))
        return true;

    _queuedDocumentIds.TryRemove(request.DocumentId, out _);
    return false;
}

public void Complete(Guid documentId) =>
    _queuedDocumentIds.TryRemove(documentId, out _);
```

- [ ] **Step 2: Recover Processing Documents before consumption**

At the beginning of `ExecuteAsync`, create a scope and query:

```csharp
var processingDocuments = await unitOfWork.Documents.Query()
    .Where(document => document.Status == DocumentStatus.Processing
        && document.LifecycleStatus == DocumentLifecycleStatus.Active)
    .AsNoTracking()
    .ToListAsync(stoppingToken);
```

For each Document:

- Convert `/uploads/{relativePath}` to the storage-relative path.
- Resolve it through `IFileStorageService.ResolveFullPath`.
- If the file exists, `TryEnqueue` a new request.
- If missing, load the tracked Document, set Failed/ErrorMessage, and save.

Do not perform extraction or embedding during the recovery scan.

- [ ] **Step 3: Release deduplication in a finally block**

Wrap each dequeue iteration:

```csharp
try
{
    await ProcessDocumentAsync(request, stoppingToken);
}
catch (Exception exception)
{
    // Existing contained failure handling.
}
finally
{
    _queue.Complete(request.DocumentId);
}
```

- [ ] **Step 4: Update every queue producer**

Replace every `EnqueueAsync` call found by:

```powershell
rg -n "EnqueueAsync" AIStudyHub.API AIStudyHub.Business
```

with `TryEnqueue`:

- Upload/reprocess returns 202 after durable persistence even when the same
  Document is already queued.
- Admin reindex increments its returned count only when `TryEnqueue` returns
  `true` and resolves source paths through `IFileStorageService`.
- `DocumentReindexWorker` calls `FailClaimAsync` with a controlled
  `Document is already queued.` message when enqueue returns `false`, so a
  durable reindex claim is not stranded.

Expected after the edit: no active `EnqueueAsync` reference remains.

- [ ] **Step 5: Build and commit**

```powershell
dotnet build AIStudyHub.slnx --no-restore
git add AIStudyHub.Business/Services/DocumentProcessingQueue.cs AIStudyHub.Business/Workers/DocumentBackgroundProcessor.cs AIStudyHub.Business/Workers/DocumentReindexWorker.cs AIStudyHub.API/Controllers/AdminController.cs
git diff --cached --check
git commit -m "refactor: recover background document processing"
```

---

### Task 4: Thin the Document Controller

**Files:**
- Modify: `AIStudyHub.API/Controllers/DocumentController.cs`

**Interfaces:**
- Consumes: `IDocumentUploadService`
- Removes direct upload dependencies on processing, embeddings, storage, options, and UnitOfWork where no other endpoint needs them

- [ ] **Step 1: Delegate upload**

Replace upload business logic with:

```csharp
await using var content = request.File.OpenReadStream();
var result = await _uploadService.UploadAsync(
    new DocumentUploadRequest(
        GetCurrentUserId(),
        request.SubjectId,
        request.Title,
        request.File.FileName,
        request.File.ContentType,
        request.File.Length,
        content),
    cancellationToken);

return Accepted(result);
```

Do not catch expected validation, size, ownership, quota, or state exceptions;
let the global middleware translate them.

- [ ] **Step 2: Delegate reprocess**

Call:

```csharp
var result = await _uploadService.ReprocessAsync(
    id,
    GetCurrentUserId(),
    cancellationToken);
return Accepted(result);
```

- [ ] **Step 3: Remove now-unused controller fields/usings**

Use compiler errors and `rg` to remove only dependencies no longer used by other Document endpoints. Do not refactor unrelated download/share/trash behavior.

- [ ] **Step 4: Build and commit**

```powershell
dotnet build AIStudyHub.slnx --no-restore
git add AIStudyHub.API/Controllers/DocumentController.cs
git diff --cached --check
git commit -m "refactor: keep document upload controller thin"
```

---

### Task 5: Standardize Required Generation DTOs and Validation

**Files:**
- Modify: `AIStudyHub.Business/DTOs/Quizzes/AiGenerationDtos.cs`
- Modify: `AIStudyHub.Business/DTOs/Flashcards/FlashcardDtos.cs`
- Modify: `AIStudyHub.Business/Validators/Quizzes/QuizValidators.cs`
- Modify: `AIStudyHub.Business/Validators/Flashcards/FlashcardValidators.cs`
- Modify: `AIStudyHub.API/Controllers/AIController.cs`

**Interfaces:**
- Produces:

```csharp
public sealed record CreateQuizRequestViaAiDto(
    [property: JsonRequired] int NumberOfQuestions);

public sealed record CreateFlashcardsViaAiRequestDto(
    [property: JsonRequired] int NumberOfFlashcards);
```

- [ ] **Step 1: Normalize DTO names and remove defaults**

Rename `CreateQuizRequestViaAIDto` to `CreateQuizRequestViaAiDto`. Use the exact
records above, import `System.Text.Json.Serialization`, and update all references.

- [ ] **Step 2: Add MVC validators**

Add:

```csharp
public sealed class CreateQuizRequestViaAiValidator
    : AbstractValidator<CreateQuizRequestViaAiDto>
{
    public CreateQuizRequestViaAiValidator()
    {
        RuleFor(request => request.NumberOfQuestions)
            .InclusiveBetween(1, 20);
    }
}

public sealed class CreateFlashcardsViaAiValidator
    : AbstractValidator<CreateFlashcardsViaAiRequestDto>
{
    public CreateFlashcardsViaAiValidator()
    {
        RuleFor(request => request.NumberOfFlashcards)
            .InclusiveBetween(1, 20);
    }
}
```

A missing JSON property fails JSON binding because of `JsonRequired`. Explicit
`0` or `21` fails FluentValidation. A fractional number fails JSON binding.
Every invalid shape returns 400.

- [ ] **Step 3: Remove duplicate controller range logic**

Let FluentValidation handle range errors. Keep JWT extraction and call the AI
services. Remove broad catches that turn known exceptions into 500; retain
logging only for unexpected errors that are rethrown to middleware.

- [ ] **Step 4: Build and commit**

```powershell
dotnet build AIStudyHub.slnx --no-restore
git add AIStudyHub.Business/DTOs/Quizzes/AiGenerationDtos.cs AIStudyHub.Business/DTOs/Flashcards/FlashcardDtos.cs AIStudyHub.Business/Validators/Quizzes/QuizValidators.cs AIStudyHub.Business/Validators/Flashcards/FlashcardValidators.cs AIStudyHub.API/Controllers/AIController.cs
git diff --cached --check
git commit -m "refactor: require AI generation counts"
```

---

### Task 6: Enforce Ownership, Readiness, and Exact Persistence

**Files:**
- Create: `AIStudyHub.Business/Exceptions/ExactGenerationCountException.cs`
- Modify: `AIStudyHub.API/Middleware/GlobalExceptionMiddleware.cs`
- Modify: `AIStudyHub.Business/AI/Generators/QuizAiService.cs`
- Modify: `AIStudyHub.Business/AI/Generators/FlashcardAiService.cs`
- Modify: `AIStudyHub.Business/Interfaces/AI/Generators/IQuizAiService.cs`

**Interfaces:**
- Produces:

```csharp
public sealed class ExactGenerationCountException : Exception
{
    public int RequestedCount { get; }
    public int GeneratedCount { get; }
}
```

- Maps `ExactGenerationCountException` to HTTP 422.

- [ ] **Step 1: Add the exact-count exception and middleware mapping**

The message must be:

```text
AI generated {generated} valid items but {requested} were required.
```

Return a JSON error with status 422 without a stack trace.

- [ ] **Step 2: Add common Document guards in both generators**

After loading the Document:

```csharp
if (document is null || document.UserId != userId)
    throw new KeyNotFoundException("Document not found.");

if (document.Status != DocumentStatus.Done)
    throw new InvalidOperationException(
        "Document must finish processing before AI generation.");
```

After loading payloads, reject an empty valid context with
`InvalidOperationException("Document has no processed content.")`.

- [ ] **Step 3: Make Quiz persistence exact**

Replace `maxBatches = requestedCount * 3` plus nested retry loops with one explicit
budget:

```csharp
const int MaxModelCalls = 4;
```

Each call asks only for the remaining valid items plus the existing small buffer,
capped at the generator's batch size. Accumulate and deduplicate valid questions
after every call. A parse failure consumes one call. The lower-level batch helper
must not start hidden retries outside this shared budget.

Estimate the quota preflight with at most `MaxModelCalls`; record the actual
tokens from every consumed call.

After the budget ends and before `PersistQuizAsync`:

```csharp
if (allQuestions.Count != request.NumberOfQuestions)
{
    await RecordConsumedTokensAsync(...);
    throw new ExactGenerationCountException(
        request.NumberOfQuestions,
        allQuestions.Count);
}
```

Update every old `request.numberOfQuestions` reference to
`request.NumberOfQuestions`. No Quiz/Question/Answer row may be added before this
guard.

- [ ] **Step 4: Make Flashcard persistence exact**

Use the same `MaxModelCalls = 4` total-call budget, including malformed-output
retries. Each next prompt requests only the missing count, includes the accepted
fronts in its avoid block, and deduplicates normalized fronts.

Before creating entity rows:

```csharp
if (flashcards.Count != request.NumberOfFlashcards)
{
    await RecordConsumedTokensAsync(...);
    throw new ExactGenerationCountException(
        request.NumberOfFlashcards,
        flashcards.Count);
}
```

Do not call `AddAsync` for partial results.

- [ ] **Step 5: Record token usage on success and exact-count failure once**

Extract a local helper or a private method in each generator so actual input and
output tokens from all model calls are recorded exactly once. Do not double-record
on the successful path, parse-failure path, or exact-count failure path.

- [ ] **Step 6: Build, inspect, and commit**

```powershell
rg -n "numberOfQuestions|NumberOfFlashcards = 10|Generated.*Requested" AIStudyHub.API AIStudyHub.Business -g "*.cs"
dotnet build AIStudyHub.slnx --no-restore
git add AIStudyHub.Business/Exceptions/ExactGenerationCountException.cs AIStudyHub.API/Middleware/GlobalExceptionMiddleware.cs AIStudyHub.Business/AI/Generators/QuizAiService.cs AIStudyHub.Business/AI/Generators/FlashcardAiService.cs AIStudyHub.Business/Interfaces/AI/Generators/IQuizAiService.cs
git diff --cached --check
git commit -m "fix: persist exact AI generation counts"
```

---

### Task 7: Update Upload and Generation Documentation

**Files:**
- Modify: `README.md`
- Modify: `AGENT.md`
- Modify: `ARCHITECTURE.md`
- Modify: `docs/FRONTEND_GUIDE.md`

- [ ] **Step 1: Document exact upload behavior**

Document:

- File-content maximum 5,242,880 bytes.
- 413 response above the limit.
- 202 response after persistence/queueing.
- Background Processing/Done/Failed states.
- Recovery after application restart.

- [ ] **Step 2: Document generation contract**

Remove the Flashcard default count. Document required `numberOfQuestions` and
`numberOfFlashcards`, inclusive 1 through 20, exact result behavior, Document
ownership, and Document Done requirement.

- [ ] **Step 3: Audit stale active claims**

```powershell
rg -n -i "20MB|50MB|default 10|numberOfQuestions|numberOfFlashcards|in-memory channel|MaxFileSizeBytes" README.md AGENT.md ARCHITECTURE.md docs/FRONTEND_GUIDE.md
```

Correct every active contradiction.

- [ ] **Step 4: Build and commit**

```powershell
dotnet build AIStudyHub.slnx --no-restore
git add README.md AGENT.md ARCHITECTURE.md docs/FRONTEND_GUIDE.md
git diff --cached --check
git commit -m "docs: describe upload and generation contracts"
```

---

## Manual Verification Handoff

Give these flows to the repository owner; do not execute them as smoke tests:

1. Upload a file of exactly 5,242,880 bytes and confirm 202.
2. Upload a file of 5,242,881 bytes and confirm 413.
3. Confirm foreign and missing Subjects return 404.
4. Submit concurrent valid uploads and confirm responses do not wait for embedding.
5. Stop the app with a Document in Processing, restart, and confirm it resumes.
6. Remove a queued file before restart and confirm the Document becomes Failed.
7. Omit each generation count and confirm 400.
8. Verify 1 and 20 generate exactly 1 and 20 persisted items.
9. Verify 0, negative, fractional, and 21 return 400.
10. Attempt generation for a foreign Document and confirm 404.
11. Attempt generation before processing completes and confirm 409.
12. Force bounded AI generation to return too few valid items and confirm 422 with no partial rows.
