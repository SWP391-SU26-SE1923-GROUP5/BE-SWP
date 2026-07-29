# Learning History Details and Flashcard Deck Deletion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist and expose complete Quiz submission details, add append-only Flashcard review history with detail endpoints, and let a student delete the complete Flashcard deck generated from one owned Document.

**Architecture:** Keep `QuizSubmission` as the immutable Quiz attempt aggregate and add its missing duration column. Keep `FlashcardReview` as the current SM-2 state, while a new `FlashcardReviewAttempt` entity stores one immutable snapshot for every review action. All history queries derive the student ID from JWT. A deck is the complete set of Flashcards whose `DocumentId` matches one Document; deleting it is an owner-only Business operation and relies on configured cascades for review state/history.

**Tech Stack:** ASP.NET Core 8, C# 12, FluentValidation 12, AutoMapper 16, EF Core 8, SQL Server.

## Global Constraints

- Do not recreate `AIStudyHub.Tests` or create any test project/file.
- Do not run unit tests, integration tests, or smoke tests.
- Use `dotnet build AIStudyHub.slnx --no-restore` for agent verification.
- The repository owner performs all functional verification manually.
- A new migration is allowed, but every existing migration is immutable.
- Never edit an existing migration or run `dotnet ef migrations remove` against a committed migration.
- Do not apply the new migration or delete database rows without separate explicit authorization.
- History rows are append-only through the public API: no edit or delete endpoint.
- Preserve the existing single-card delete endpoint in addition to deck deletion.

---

## File Structure

### Quiz history detail

- Modify `AIStudyHub.Data/Entities/QuizSubmission.cs` to persist `DurationSeconds`.
- Modify `AIStudyHub.Data/Configurations/EntityConfigurations.cs` to configure the nullable duration column.
- Modify `AIStudyHub.Business/DTOs/QuizSubmissions/QuizSubmissionDtos.cs` to add detailed question/option response contracts.
- Modify `AIStudyHub.Business/Validators/QuizSubmissions/QuizSubmissionValidators.cs` to validate duration and answer JSON.
- Create `AIStudyHub.Business/Exceptions/CorruptedQuizSubmissionException.cs`.
- Modify `AIStudyHub.API/Middleware/GlobalExceptionMiddleware.cs` with its stable server-error response.
- Modify `AIStudyHub.Business/Interfaces/Services/IQuizSubmissionService.cs` to make detail/history ownership explicit.
- Modify `AIStudyHub.Business/Services/ModuleServices.cs` to persist duration and project owner-scoped history details.
- Modify `AIStudyHub.API/Controllers/QuizSubmissionController.cs` to expose the owner-scoped detail endpoint.
- Modify `AIStudyHub.API/Controllers/QuizController.cs` only where its existing history route delegates to the changed service contract.
- Modify `AIStudyHub.Business/Mappings/ApplicationMappingProfile.cs` to map persisted duration.

### Flashcard review history

- Create `AIStudyHub.Data/Entities/FlashcardReviewAttempt.cs`.
- Modify `AIStudyHub.Data/ApplicationDbContext.cs` and `AIStudyHub.Data/Configurations/EntityConfigurations.cs`.
- Modify `AIStudyHub.Data/Interfaces/IUnitOfWork.cs` and the repository-backed Unit of Work implementation located during execution.
- Modify `AIStudyHub.Business/DTOs/FlashcardReviews/FlashcardReviewDtos.cs` with history summary/detail contracts.
- Create `AIStudyHub.Business/Validators/FlashcardReviews/FlashcardReviewValidators.cs`.
- Modify `AIStudyHub.Business/Interfaces/Services/IFlashcardReviewService.cs`.
- Modify `AIStudyHub.Business/Services/FlashcardReviewService.cs` to append attempt snapshots and query them.
- Modify `AIStudyHub.API/Controllers/FlashcardReviewController.cs` to expose history list/detail endpoints.

### Whole-deck deletion

- Modify `AIStudyHub.Business/Interfaces/Services/IFlashcardService.cs`.
- Modify the `FlashcardService` implementation in `AIStudyHub.Business/Services/ModuleServices.cs`.
- Modify `AIStudyHub.API/Controllers/FlashcardController.cs`.

### Migration and documentation

- Generate a new `AddLearningHistoryDetails` migration under `AIStudyHub.Data/Migrations/`.
- Allow EF Core to update `AIStudyHub.Data/Migrations/ApplicationDbContextModelSnapshot.cs`.
- Modify `docs/api-contract.md`.
- Modify `docs/backend-feature-status.md`.
- Modify `AIStudyHub.API/AIStudyHub.API.http`.

---

## Task 1: Persist Quiz Duration and Tighten Submission Validation

**Files:**

- Modify: `AIStudyHub.Data/Entities/QuizSubmission.cs`
- Modify: `AIStudyHub.Data/Configurations/EntityConfigurations.cs`
- Modify: `AIStudyHub.Business/Validators/QuizSubmissions/QuizSubmissionValidators.cs`
- Modify: `AIStudyHub.Business/Services/ModuleServices.cs`
- Modify: `AIStudyHub.Business/Mappings/ApplicationMappingProfile.cs`

- [ ] **Step 1: Add the missing duration property**

Add this nullable property to `QuizSubmission`:

```csharp
public int? DurationSeconds { get; set; }
```

Configure it with column name `duration_seconds`. The database column stays nullable so existing demo submissions can migrate without fabricated timing data.

- [ ] **Step 2: Define the submission boundary**

Keep `DurationSeconds` optional for compatibility with existing clients, but when supplied require:

```text
1 <= DurationSeconds <= 86,400
```

Validate that `Answers` is non-empty JSON with the same answer object shape that the scoring code consumes. Reject malformed JSON as a request-validation error instead of allowing deserialization to fail inside the service.

The validator parses:

```csharp
Dictionary<string, string>
```

Every present key must be a non-empty Question GUID and every present selected
option must be non-blank. Missing question keys, including an empty dictionary,
remain valid and represent unanswered questions.

- [ ] **Step 3: Persist the received value**

In the canonical `SubmitAsync` path, and in any remaining
`new QuizSubmission` initializer found by `rg`, copy:

```csharp
DurationSeconds = request.DurationSeconds
```

Replace every hard-coded `null` duration in history projections with `submission.DurationSeconds`. Ensure the response mapping reads the entity property rather than inventing a value.

- [ ] **Step 4: Build**

Run:

```powershell
dotnet build AIStudyHub.slnx --no-restore
```

Expected: build succeeds; no new warning is introduced.

- [ ] **Step 5: Commit the task**

```powershell
git add AIStudyHub.Data/Entities/QuizSubmission.cs AIStudyHub.Data/Configurations/EntityConfigurations.cs AIStudyHub.Business/Validators/QuizSubmissions/QuizSubmissionValidators.cs AIStudyHub.Business/Services/ModuleServices.cs AIStudyHub.Business/Mappings/ApplicationMappingProfile.cs
git diff --cached --check
git commit -m "refactor: persist quiz attempt duration"
```

---

## Task 2: Add Owner-Scoped Quiz History Detail

**Files:**

- Modify: `AIStudyHub.Business/DTOs/QuizSubmissions/QuizSubmissionDtos.cs`
- Modify: `AIStudyHub.Business/Interfaces/Services/IQuizSubmissionService.cs`
- Modify: `AIStudyHub.Business/Services/ModuleServices.cs`
- Modify: `AIStudyHub.API/Controllers/QuizSubmissionController.cs`
- Modify: `AIStudyHub.API/Controllers/QuizController.cs`
- Create: `AIStudyHub.Business/Exceptions/CorruptedQuizSubmissionException.cs`
- Modify: `AIStudyHub.API/Middleware/GlobalExceptionMiddleware.cs`

- [ ] **Step 1: Add explicit detail response contracts**

Add contracts equivalent to:

```csharp
public sealed record QuizSubmissionOptionDetailDto(
    Guid AnswerId,
    string Text,
    bool IsSelected,
    bool IsCorrect);

public sealed record QuizSubmissionQuestionDetailDto(
    Guid QuestionId,
    string Title,
    QuestionType Type,
    int Position,
    IReadOnlyList<QuizSubmissionOptionDetailDto> Options);

public sealed record QuizSubmissionDetailDto(
    Guid Id,
    Guid QuizId,
    string QuizTitle,
    Guid DocumentId,
    string DocumentTitle,
    Guid SubjectId,
    string SubjectCode,
    string SubjectName,
    int Score,
    int MaxScore,
    int TotalCorrect,
    int? DurationSeconds,
    double PercentageScore,
    DateTime? GradedAt,
    DateTime SubmittedAt,
    IReadOnlyList<QuizSubmissionQuestionDetailDto> Questions);
```

`Text` maps from the current `Answer.SelectedOption` field. Order questions by `Position`, then options deterministically by `CreatedAt` and `Id`.

- [ ] **Step 2: Make service ownership explicit**

Replace the unsafe public detail signature:

```csharp
Task<QuizSubmissionResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
```

with:

```csharp
Task<QuizSubmissionDetailDto?> GetOwnedDetailAsync(
    Guid submissionId,
    Guid userId,
    CancellationToken cancellationToken = default);
```

Change `GetQuizHistoryAsync` to accept `Guid userId`. Its query must include:

```csharp
submission.UserId == userId && submission.QuizId == quizId
```

Keep the Admin list method unchanged. Keep `GetByUserAndQuizAsync` only if an existing route still consumes it; it already requires both IDs.

- [ ] **Step 3: Deserialize selections and build the detail**

Load the submission in one owner-filtered query with:

```text
Quiz
  -> Document
    -> Subject
  -> Questions
    -> Answers
```

Parse the stored `Answers` using one shared helper with the current persisted
shape:

```csharp
Dictionary<string, string> // question ID -> selected option text
```

Build selection flags with the same ordinal text comparison used during grading,
so detail and score cannot disagree. The existing payload does not persist Answer
IDs; do not pretend that it does.

If an old row contains malformed `Answers`, log only the submission ID and throw:

```csharp
new CorruptedQuizSubmissionException(submission.Id)
```

Map it to HTTP 500 with stable error code `CorruptedQuizSubmission` and message
`Stored quiz answers are invalid.` Do not include the ID, raw JSON, exception
type, or serializer stack trace in the response.

- [ ] **Step 4: Secure the controller routes**

Change:

```http
GET /api/QuizSubmission/{id}
```

to derive `userId` from JWT and call `GetOwnedDetailAsync(id, userId, ct)`. Return:

- `401` when the identity claim is absent/invalid.
- `404` when the submission does not exist or belongs to another user.
- `200` with the full detail otherwise.

For any existing Quiz-controller history alias, pass the JWT user ID into the changed service method. Do not add an Admin override for student history.

- [ ] **Step 5: Build**

Run:

```powershell
dotnet build AIStudyHub.slnx --no-restore
```

Expected: all changed service/controller signatures compile and the detail DTO is serialized without cycles.

- [ ] **Step 6: Commit the task**

```powershell
git add AIStudyHub.Business/DTOs/QuizSubmissions/QuizSubmissionDtos.cs AIStudyHub.Business/Interfaces/Services/IQuizSubmissionService.cs AIStudyHub.Business/Services/ModuleServices.cs AIStudyHub.API/Controllers/QuizSubmissionController.cs AIStudyHub.API/Controllers/QuizController.cs AIStudyHub.Business/Exceptions/CorruptedQuizSubmissionException.cs AIStudyHub.API/Middleware/GlobalExceptionMiddleware.cs
git diff --cached --check
git commit -m "refactor: expose owned quiz history details"
```

---

## Task 3: Introduce Append-Only Flashcard Review Attempts

**Files:**

- Create: `AIStudyHub.Data/Entities/FlashcardReviewAttempt.cs`
- Modify: `AIStudyHub.Data/ApplicationDbContext.cs`
- Modify: `AIStudyHub.Data/Configurations/EntityConfigurations.cs`
- Modify: `AIStudyHub.Data/Interfaces/IUnitOfWork.cs`
- Modify: repository-backed Unit of Work implementation found with `rg -n "class UnitOfWork" AIStudyHub.Data`

- [ ] **Step 1: Define the immutable attempt entity**

Create this shape:

```csharp
public sealed class FlashcardReviewAttempt : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid FlashcardId { get; set; }
    public ReviewQuality Quality { get; set; }
    public int? TimeSpentSeconds { get; set; }
    public float PreviousEaseFactor { get; set; }
    public float ResultEaseFactor { get; set; }
    public int PreviousInterval { get; set; }
    public int ResultInterval { get; set; }
    public int PreviousRepetitions { get; set; }
    public int ResultRepetitions { get; set; }
    public DateTime PreviousNextReviewDate { get; set; }
    public DateTime ResultNextReviewDate { get; set; }
    public int XpEarned { get; set; }

    public User User { get; set; } = null!;
    public Flashcard Flashcard { get; set; } = null!;
}
```

Use `BaseEntity.CreatedAt` as the immutable review-event timestamp. Do not add a
second timestamp with competing semantics.

- [ ] **Step 2: Configure relationships and indexes**

Use table `FlashcardReviewAttempt`. Configure:

- required enum conversion matching the existing enum convention;
- nullable `time_spent_seconds`;
- required snapshot fields;
- index `(UserId, CreatedAt)`;
- index `(UserId, FlashcardId, CreatedAt)`;
- cascade from `Flashcard`;
- cascade from `User`.

Do not add a foreign key from attempt to `FlashcardReview`. The attempt is an
immutable event linked directly to the User and Flashcard; this keeps deletion
paths unambiguous:

```text
Flashcard -> FlashcardReview
Flashcard -> FlashcardReviewAttempt
```

- [ ] **Step 3: Register persistence access**

Add:

```csharp
public DbSet<FlashcardReviewAttempt> FlashcardReviewAttempts =>
    Set<FlashcardReviewAttempt>();
```

Add `IRepository<FlashcardReviewAttempt> FlashcardReviewAttempts` to `IUnitOfWork` and initialize it in the concrete implementation using the same repository pattern as adjacent entities.

- [ ] **Step 4: Build**

Run:

```powershell
dotnet build AIStudyHub.slnx --no-restore
```

Expected: the entity is discoverable and no EF relationship ambiguity remains.

- [ ] **Step 5: Commit the task**

Stage the exact Unit of Work file discovered in Step 3 together with:

```powershell
git add AIStudyHub.Data/Entities/FlashcardReviewAttempt.cs AIStudyHub.Data/ApplicationDbContext.cs AIStudyHub.Data/Configurations/EntityConfigurations.cs AIStudyHub.Data/Interfaces/IUnitOfWork.cs
git diff --cached --check
git commit -m "refactor: model flashcard review attempts"
```

---

## Task 4: Append Attempts During Review Processing

**Files:**

- Modify: `AIStudyHub.Business/Services/FlashcardReviewService.cs`
- Create: `AIStudyHub.Business/Validators/FlashcardReviews/FlashcardReviewValidators.cs`

- [ ] **Step 1: Validate optional review duration**

Create a validator for `ReviewFlashcardRequestDto`. Keep the existing Flashcard
ID and enum boundary checks, and when `TimeSpentSeconds` has a value require:

```text
1 <= TimeSpentSeconds <= 86,400
```

Repeat this guard in `ProcessReviewAsync` for non-MVC callers and return the
existing controlled failed `ServiceResult`.

- [ ] **Step 2: Enforce readable-document access before mutation**

Replace the bare Flashcard lookup with a query that loads its Document and allows the action only when:

```text
Document.UserId == userId
OR Document.ShareStatus == "public"
OR Document.DocumentShares contains userId
```

Return the same not-found style result for missing and inaccessible cards to avoid revealing private resource IDs.

- [ ] **Step 3: Capture the before-state**

Immediately before `ApplySm2`, copy:

```text
EaseFactor
Interval
Repetitions
NextReviewDate
```

After `ApplySm2`, create a `FlashcardReviewAttempt` containing the before/result
snapshots, quality, optional time, `CreatedAt = DateTime.UtcNow`, and
`XpEarned = 0`.

- [ ] **Step 4: Save state and attempt together**

Add the attempt before the first `SaveChangesAsync`. This makes the SM-2 state update and attempt append part of one EF Core save operation. If that save fails, return the existing controlled review failure and do not award XP.

Do not add an attempt when request validation or resource access fails.

- [ ] **Step 5: Reconcile XP without losing the review**

Keep the current behavior in which gamification failure does not roll back a valid review. When XP succeeds:

```csharp
attempt.XpEarned = xpEarned;
_unitOfWork.FlashcardReviewAttempts.Update(attempt);
await _unitOfWork.SaveChangesAsync(cancellationToken);
```

If this second save fails, log the attempt ID and continue returning the completed review. The history row remains valid with `XpEarned = 0`; do not duplicate the attempt.

- [ ] **Step 6: Remove the synchronous existence probe**

Replace:

```csharp
_unitOfWork.FlashcardReviews.Query().Any(...)
```

with explicit `isNewReview` state captured when the current row is loaded/created. This avoids a blocking query and makes add/update behavior deterministic.

- [ ] **Step 7: Build and commit**

```powershell
dotnet build AIStudyHub.slnx --no-restore
git add AIStudyHub.Business/Services/FlashcardReviewService.cs AIStudyHub.Business/Validators/FlashcardReviews/FlashcardReviewValidators.cs
git diff --cached --check
git commit -m "refactor: record flashcard review history"
```

---

## Task 5: Expose Flashcard History Summary and Detail

**Files:**

- Modify: `AIStudyHub.Business/DTOs/FlashcardReviews/FlashcardReviewDtos.cs`
- Modify: `AIStudyHub.Business/Interfaces/Services/IFlashcardReviewService.cs`
- Modify: `AIStudyHub.Business/Services/FlashcardReviewService.cs`
- Modify: `AIStudyHub.API/Controllers/FlashcardReviewController.cs`

- [ ] **Step 1: Define response contracts**

Add:

```csharp
public sealed record FlashcardReviewHistoryItemDto(
    Guid AttemptId,
    Guid FlashcardId,
    Guid DocumentId,
    string DocumentTitle,
    string Front,
    ReviewQuality Quality,
    int? TimeSpentSeconds,
    int XpEarned,
    DateTime ReviewedAt);

public sealed record FlashcardReviewHistoryDetailDto(
    Guid AttemptId,
    Guid FlashcardId,
    Guid DocumentId,
    string DocumentTitle,
    Guid SubjectId,
    string SubjectCode,
    string SubjectName,
    string Front,
    string Back,
    ReviewQuality Quality,
    int? TimeSpentSeconds,
    float PreviousEaseFactor,
    float ResultEaseFactor,
    int PreviousInterval,
    int ResultInterval,
    int PreviousRepetitions,
    int ResultRepetitions,
    DateTime PreviousNextReviewDate,
    DateTime ResultNextReviewDate,
    int XpEarned,
    DateTime ReviewedAt);
```

- [ ] **Step 2: Extend the service contract**

Add:

```csharp
Task<PagedResultDto<FlashcardReviewHistoryItemDto>> GetHistoryAsync(
    Guid userId,
    Guid? documentId,
    Guid? flashcardId,
    DateTime? fromDate,
    DateTime? toDate,
    PaginationParams pagination,
    CancellationToken cancellationToken = default);

Task<FlashcardReviewHistoryDetailDto?> GetHistoryDetailAsync(
    Guid userId,
    Guid attemptId,
    CancellationToken cancellationToken = default);
```

- [ ] **Step 3: Implement owner-scoped projections**

Every query starts with:

```csharp
attempt.UserId == userId
```

Then apply optional filters, count, order by `CreatedAt DESC`, apply
`Offset`/`Limit`, and project `CreatedAt` as DTO `ReviewedAt`. Detail also filters
by attempt ID and user ID in the database query and joins
`Flashcard.Document.Subject` for the approved Subject fields. Do not load another
user's attempt and filter it in memory.

- [ ] **Step 4: Add API routes**

Add:

```http
GET /api/FlashcardReview/history
GET /api/FlashcardReview/history/{attemptId}
```

List query parameters:

```text
documentId, flashcardId, fromDate, toDate, offset=0, limit=20
```

Both endpoints derive the user ID from JWT. Detail returns `404` for both absent and other-user attempts. Correct the existing stats endpoint so a student cannot supply an arbitrary `userId`: use `GET /api/FlashcardReview/stats` and the JWT identity.

- [ ] **Step 5: Build and commit**

```powershell
dotnet build AIStudyHub.slnx --no-restore
git add AIStudyHub.Business/DTOs/FlashcardReviews/FlashcardReviewDtos.cs AIStudyHub.Business/Interfaces/Services/IFlashcardReviewService.cs AIStudyHub.Business/Services/FlashcardReviewService.cs AIStudyHub.API/Controllers/FlashcardReviewController.cs
git diff --cached --check
git commit -m "refactor: expose flashcard history details"
```

---

## Task 6: Delete a Complete Flashcard Deck

**Files:**

- Modify: `AIStudyHub.Business/Interfaces/Services/IFlashcardService.cs`
- Modify: `AIStudyHub.Business/Services/ModuleServices.cs`
- Modify: `AIStudyHub.API/Controllers/FlashcardController.cs`

- [ ] **Step 1: Add an owner-explicit Business operation**

Add:

```csharp
Task<int> DeleteDeckAsync(
    Guid documentId,
    Guid userId,
    CancellationToken cancellationToken = default);
```

The returned integer is the number of Flashcards removed.

- [ ] **Step 2: Implement deck semantics in the service**

In one owner-filtered query, verify:

```csharp
document.Id == documentId && document.UserId == userId
```

Treat missing and not-owned Documents identically. Load all Flashcards for that Document, remove them, and call `SaveChangesAsync` once. Configured cascades remove associated `FlashcardReview` and `FlashcardReviewAttempt` rows.

Do not delete or modify:

```text
Document
Quiz / Question / Answer / QuizSubmission
StudyLog
Subject
uploaded file
vector chunks
```

Deleting an existing empty deck is idempotent and returns `0`.

- [ ] **Step 3: Add the deck route**

Add:

```http
DELETE /api/Flashcard/by-document/{documentId}
```

Derive `userId` from JWT and delegate authorization to the owner-explicit service. Return `204 No Content` after a successful delete, including an already-empty deck. Return `404` when the Document is missing or belongs to another user.

Keep:

```http
DELETE /api/Flashcard/{id}
```

for single-card deletion.

- [ ] **Step 4: Build and commit**

```powershell
dotnet build AIStudyHub.slnx --no-restore
git add AIStudyHub.Business/Interfaces/Services/IFlashcardService.cs AIStudyHub.Business/Services/ModuleServices.cs AIStudyHub.API/Controllers/FlashcardController.cs
git diff --cached --check
git commit -m "refactor: delete complete flashcard decks"
```

---

## Task 7: Generate the New Learning-History Migration

**Files:**

- Create: `AIStudyHub.Data/Migrations/<timestamp>_AddLearningHistoryDetails.cs`
- Create: `AIStudyHub.Data/Migrations/<timestamp>_AddLearningHistoryDetails.Designer.cs`
- Modify: `AIStudyHub.Data/Migrations/ApplicationDbContextModelSnapshot.cs`

- [ ] **Step 1: Confirm old migrations remain byte-for-byte untouched**

Run:

```powershell
git status --short AIStudyHub.Data/Migrations
```

Expected before generation: no migration changes.

- [ ] **Step 2: Generate, never hand-edit an existing migration**

Use the repository's required design-time environment values, then run:

```powershell
dotnet ef migrations add AddLearningHistoryDetails --project AIStudyHub.Data/AIStudyHub.Data.csproj --startup-project AIStudyHub.API/AIStudyHub.API.csproj
```

This is generation only. Do not run `database update`.

- [ ] **Step 3: Inspect the generated migration**

The `Up` method must contain only:

- nullable `duration_seconds` on `QuizSubmission`;
- create `FlashcardReviewAttempt`;
- the approved foreign keys and indexes.

The `Down` method may reverse only this new migration. Verify Git shows exactly two new migration files plus the snapshot; no older migration may be modified.

- [ ] **Step 4: Build and inspect model state**

Run:

```powershell
dotnet build AIStudyHub.slnx --no-restore
dotnet ef migrations has-pending-model-changes --project AIStudyHub.Data/AIStudyHub.Data.csproj --startup-project AIStudyHub.API/AIStudyHub.API.csproj
```

Expected: build succeeds and EF reports no pending model changes. Do not apply the migration.

- [ ] **Step 5: Commit the generated migration**

Stage only the two newly generated migration files and the snapshot:

```powershell
git add AIStudyHub.Data/Migrations
git diff --cached --name-status
git diff --cached --check
git commit -m "refactor: add learning history schema"
```

Before committing, stop if any pre-existing timestamped migration appears as modified or deleted.

---

## Task 8: Update Contracts and Prepare Manual Verification

**Files:**

- Modify: `docs/api-contract.md`
- Modify: `docs/backend-feature-status.md`
- Modify: `AIStudyHub.API/AIStudyHub.API.http`

- [ ] **Step 1: Document all changed contracts**

Document:

- `GET /api/QuizSubmission/{id}` returns complete owned attempt detail;
- Quiz history list/detail includes persisted `DurationSeconds`;
- Flashcard history list and detail filters;
- stats uses the current JWT user;
- deck deletion definition, owner restriction, cascaded review data, and preserved resources.

- [ ] **Step 2: Add manual request examples**

Add HTTP examples for:

1. submitting a Quiz with `DurationSeconds`;
2. listing and opening that Quiz submission;
3. reviewing the same Flashcard twice;
4. listing and opening both immutable attempt records;
5. deleting the Document's whole Flashcard deck;
6. confirming Quiz history remains available afterward.

- [ ] **Step 3: Run final repository verification**

Run:

```powershell
dotnet build AIStudyHub.slnx --no-restore
git diff --check
git status --short
```

Expected: build succeeds, no whitespace error, no test project/file exists, and only intended source/documentation/new-migration changes remain.

- [ ] **Step 4: Commit documentation**

```powershell
git add docs/api-contract.md docs/backend-feature-status.md AIStudyHub.API/AIStudyHub.API.http
git diff --cached --check
git commit -m "docs: describe learning history and deck deletion"
```

---

## Manual Acceptance Checklist

The repository owner performs these checks after explicitly applying the new migration in their own test database:

- A Quiz submission with duration persists the exact supplied seconds.
- The history list displays the persisted duration, not `null`.
- Opening a submission shows every question, every option, selected options, and correct options.
- A user receives `404` when requesting another user's submission ID.
- Two reviews of one Flashcard create two ordered history attempts while one current SM-2 state row remains.
- An inaccessible private Flashcard cannot be reviewed by another student.
- Flashcard history detail includes the before/after schedule snapshots and the submitted quality.
- A user receives `404` for another user's review-attempt ID.
- Flashcard stats are based only on the authenticated user.
- Deleting a deck removes every Flashcard, current review row, and attempt row for that Document.
- Deleting a deck preserves the Document, uploaded file, vector chunks, Quiz data, Quiz history, StudyLogs, and Subject.
- Deleting another user's deck is rejected without revealing whether the private Document exists.
