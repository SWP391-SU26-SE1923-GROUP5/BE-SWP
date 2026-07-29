# Learning History and Flashcard Decks Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Require generation counts, expose secure Quiz and Flashcard history details, and delete complete Flashcard decks.

**Architecture:** Quiz detail reconstructs per-question results from persisted answers and current Quiz structure. Flashcard reviews append immutable attempt rows while retaining the current SM-2 schedule row.

**Tech Stack:** ASP.NET Core 8, EF Core 8, FluentValidation, xUnit, Moq

## Global Constraints

- `NumberOfQuestions` and `NumberOfFlashcards` are required integers from 1 through 20.
- History is available only to the authenticated owner; foreign rows return 404.
- A deck is all Flashcards for one Document.
- Deck deletion does not delete the Document or its Quizzes.
- Existing migration files remain unchanged.

---

### Task 1: Require and validate generation counts

**Files:**
- Modify: `AIStudyHub.Business/DTOs/Quizzes/AiGenerationDtos.cs`
- Modify: `AIStudyHub.Business/DTOs/Flashcards/FlashcardDtos.cs`
- Modify: `AIStudyHub.Business/Validators/Quizzes/QuizValidators.cs`
- Modify: `AIStudyHub.Business/Validators/Flashcards/FlashcardValidators.cs`
- Modify: `AIStudyHub.API/Controllers/AIController.cs`
- Create: `AIStudyHub.Tests/Services/AiGenerationCountValidatorTests.cs`

**Interfaces:**
- Produces: `CreateQuizRequestViaAIDto(int? NumberOfQuestions)`
- Produces: `CreateFlashcardsViaAiRequestDto(int? NumberOfFlashcards)`
- Services consume `.Value` only after explicit null/range guards

- [ ] **Step 1: Write validator boundary tests**

```csharp
[Theory]
[InlineData(null, false)]
[InlineData(0, false)]
[InlineData(1, true)]
[InlineData(20, true)]
[InlineData(21, false)]
public void FlashcardCount_ValidatesRequiredRange(int? count, bool expected)
{
    var result = _flashcardValidator.Validate(new CreateFlashcardsViaAiRequestDto(count));
    Assert.Equal(expected, result.IsValid);
}
```

Add the identical theory for Quiz.

- [ ] **Step 2: Run and confirm failure**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --no-restore --filter FullyQualifiedName~AiGenerationCountValidatorTests`

Expected: FAIL because validators and nullable required contracts do not exist.

- [ ] **Step 3: Implement explicit required contracts**

```csharp
RuleFor(x => x.NumberOfQuestions)
    .NotNull()
    .InclusiveBetween(1, 20);

RuleFor(x => x.NumberOfFlashcards)
    .NotNull()
    .InclusiveBetween(1, 20);
```

Rename `numberOfQuestions` to `NumberOfQuestions`, remove the Flashcard default, and retain service guards that throw `ArgumentOutOfRangeException`. Catch that exception as 400 in both controller actions.

- [ ] **Step 4: Run validator and generator tests**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --no-restore --filter "FullyQualifiedName~AiGenerationCountValidatorTests|FullyQualifiedName~QuizAi|FullyQualifiedName~FlashcardAi"`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add AIStudyHub.Business/DTOs/Quizzes/AiGenerationDtos.cs AIStudyHub.Business/DTOs/Flashcards/FlashcardDtos.cs AIStudyHub.Business/Validators/Quizzes/QuizValidators.cs AIStudyHub.Business/Validators/Flashcards/FlashcardValidators.cs AIStudyHub.API/Controllers/AIController.cs AIStudyHub.Tests/Services/AiGenerationCountValidatorTests.cs
git commit -m "fix: require ai generation counts"
```

### Task 2: Persist Quiz duration and define detail DTOs

**Files:**
- Modify: `AIStudyHub.Data/Entities/QuizSubmission.cs`
- Modify: `AIStudyHub.Data/Configurations/EntityConfigurations.cs`
- Modify: `AIStudyHub.Business/DTOs/QuizSubmissions/QuizSubmissionDtos.cs`
- Modify: `AIStudyHub.Business/Interfaces/Services/IQuizSubmissionService.cs`
- Create: `AIStudyHub.Tests/Services/QuizSubmissionDetailDtoTests.cs`

**Interfaces:**
- Produces: `QuizSubmission.DurationSeconds : int?`
- Produces: `GetDetailAsync(Guid submissionId, Guid userId, CancellationToken ct)`
- Produces: `QuizSubmissionDetailDto` and `QuizQuestionResultDto`

- [ ] **Step 1: Add a failing DTO-shape test**

```csharp
[Fact]
public void QuizDetail_ContainsPerQuestionResult()
{
    var property = typeof(QuizSubmissionDetailDto)
        .GetProperty(nameof(QuizSubmissionDetailDto.Questions));
    Assert.Equal(typeof(IReadOnlyList<QuizQuestionResultDto>), property!.PropertyType);
}
```

- [ ] **Step 2: Run and confirm failure**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --no-restore --filter FullyQualifiedName~QuizSubmissionDetailDtoTests`

Expected: FAIL because the detail DTO does not exist.

- [ ] **Step 3: Add exact detail records**

```csharp
public sealed record QuizQuestionResultDto(
    Guid QuestionId,
    string Question,
    int Position,
    IReadOnlyList<string> Options,
    string? SelectedOption,
    string CorrectOption,
    bool IsCorrect);

public sealed record QuizSubmissionDetailDto(
    Guid Id,
    Guid QuizId,
    string QuizTitle,
    Guid DocumentId,
    string DocumentTitle,
    string SubjectCode,
    int Score,
    int MaxScore,
    int TotalCorrect,
    double PercentageScore,
    int? DurationSeconds,
    DateTime SubmittedAt,
    DateTime? GradedAt,
    IReadOnlyList<QuizQuestionResultDto> Questions);
```

- [ ] **Step 4: Run DTO tests**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --no-restore --filter FullyQualifiedName~QuizSubmissionDetailDtoTests`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add AIStudyHub.Data/Entities/QuizSubmission.cs AIStudyHub.Data/Configurations/EntityConfigurations.cs AIStudyHub.Business/DTOs/QuizSubmissions/QuizSubmissionDtos.cs AIStudyHub.Business/Interfaces/Services/IQuizSubmissionService.cs AIStudyHub.Tests/Services/QuizSubmissionDetailDtoTests.cs
git commit -m "feat: define quiz submission details"
```

### Task 3: Implement secure Quiz history detail

**Files:**
- Modify: `AIStudyHub.Business/Services/ModuleServices.cs`
- Modify: `AIStudyHub.API/Controllers/QuizSubmissionController.cs`
- Modify: `AIStudyHub.Business/Mappings/ApplicationMappingProfile.cs`
- Create: `AIStudyHub.Tests/Services/QuizSubmissionHistoryDetailTests.cs`

**Interfaces:**
- Consumes: `GetDetailAsync(Guid submissionId, Guid userId, CancellationToken ct)`
- Produces: `GET /api/QuizSubmission/{id}` owner-only detail

- [ ] **Step 1: Write failing detail and ownership tests**

```csharp
[Fact]
public async Task GetDetailAsync_ReturnsSelectedAndCorrectAnswers()
{
    var detail = await _service.GetDetailAsync(_submissionId, _ownerId);
    Assert.Equal("B", detail!.Questions[0].SelectedOption);
    Assert.Equal("B", detail.Questions[0].CorrectOption);
    Assert.True(detail.Questions[0].IsCorrect);
}

[Fact]
public async Task GetDetailAsync_ForForeignOwner_ReturnsNull()
{
    Assert.Null(await _service.GetDetailAsync(_submissionId, Guid.NewGuid()));
}
```

- [ ] **Step 2: Run and confirm failure**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --no-restore --filter FullyQualifiedName~QuizSubmissionHistoryDetailTests`

Expected: FAIL because detail reconstruction and ownership filtering do not exist.

- [ ] **Step 3: Implement one eager-loaded query and defensive parsing**

Load `Quiz.Document.Subject`, ordered Questions, and Answers with `AsNoTracking`. Filter by both submission ID and user ID. Parse:

```csharp
Dictionary<string, string> submitted;
try
{
    submitted = JsonSerializer.Deserialize<Dictionary<string, string>>(submission.Answers)
        ?? new Dictionary<string, string>();
}
catch (JsonException exception)
{
    throw new InvalidDataException("Stored quiz answers are malformed.", exception);
}
```

Persist `request.DurationSeconds` during submit. Update the controller's existing ID route to call `GetDetailAsync(id, currentUserId)` and return 404 for null.

- [ ] **Step 4: Run Quiz history tests**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --no-restore --filter FullyQualifiedName~QuizSubmissionHistoryDetailTests`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add AIStudyHub.Business/Services/ModuleServices.cs AIStudyHub.API/Controllers/QuizSubmissionController.cs AIStudyHub.Business/Mappings/ApplicationMappingProfile.cs AIStudyHub.Tests/Services/QuizSubmissionHistoryDetailTests.cs
git commit -m "feat: return secure quiz history details"
```

### Task 4: Add append-only Flashcard review attempts

**Files:**
- Create: `AIStudyHub.Data/Entities/FlashcardReviewAttempt.cs`
- Modify: `AIStudyHub.Data/Entities/Flashcard.cs`
- Modify: `AIStudyHub.Data/Entities/User.cs`
- Modify: `AIStudyHub.Data/Configurations/EntityConfigurations.cs`
- Modify: `AIStudyHub.Data/ApplicationDbContext.cs`
- Modify: `AIStudyHub.Data/Interfaces/IUnitOfWork.cs`
- Modify: `AIStudyHub.Data/Repositories/UnitOfWork.cs`
- Modify: `AIStudyHub.Business/DTOs/FlashcardReviews/FlashcardReviewDtos.cs`
- Create: `AIStudyHub.Tests/Services/FlashcardReviewAttemptModelTests.cs`

**Interfaces:**
- Produces: `FlashcardReviewAttempt` append-only history row
- Produces: `FlashcardReviewHistoryDto` and `FlashcardReviewDetailDto`

- [ ] **Step 1: Write a failing model test**

```csharp
[Fact]
public void AttemptModel_IndexesUserReviewTime()
{
    var options = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlite("DataSource=:memory:")
        .Options;
    using var context = new ApplicationDbContext(options);
    var entity = context.Model.FindEntityType(typeof(FlashcardReviewAttempt))!;
    Assert.Contains(entity.GetIndexes(), index =>
        index.Properties.Select(p => p.Name).SequenceEqual(
            new[] { nameof(FlashcardReviewAttempt.UserId), nameof(FlashcardReviewAttempt.ReviewedAt) }));
}
```

- [ ] **Step 2: Run and confirm failure**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --no-restore --filter FullyQualifiedName~FlashcardReviewAttemptModelTests`

Expected: FAIL because the attempt entity does not exist.

- [ ] **Step 3: Implement the attempt entity**

Add required fields from the approved design using `float` for ease factors, `int` for intervals/repetitions/XP, nullable `int` for time, and `DateTime` for reviewed/next-review timestamps. Configure cascade from Flashcard and restrict from User.

- [ ] **Step 4: Run model tests**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --no-restore --filter FullyQualifiedName~FlashcardReviewAttemptModelTests`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add AIStudyHub.Data AIStudyHub.Business/DTOs/FlashcardReviews/FlashcardReviewDtos.cs AIStudyHub.Tests/Services/FlashcardReviewAttemptModelTests.cs
git commit -m "feat: persist flashcard review attempts"
```

### Task 5: Record attempts and expose Flashcard history

**Files:**
- Modify: `AIStudyHub.Business/Interfaces/Services/IFlashcardReviewService.cs`
- Modify: `AIStudyHub.Business/Services/FlashcardReviewService.cs`
- Modify: `AIStudyHub.API/Controllers/FlashcardReviewController.cs`
- Create: `AIStudyHub.Tests/Services/FlashcardReviewHistoryTests.cs`

**Interfaces:**
- Produces: `GetHistoryAsync(Guid userId, Guid? documentId, Guid? flashcardId, DateTime? from, DateTime? to, PaginationParams params, CancellationToken ct)`
- Produces: `GetHistoryDetailAsync(Guid attemptId, Guid userId, CancellationToken ct)`

- [ ] **Step 1: Write failing append/list/detail tests**

```csharp
[Fact]
public async Task ProcessReviewAsync_AppendsAttemptForEveryReview()
{
    await _service.ProcessReviewAsync(_userId, _flashcardId, ReviewQuality.Good);
    await _service.ProcessReviewAsync(_userId, _flashcardId, ReviewQuality.Hard);
    Assert.Equal(2, await _context.FlashcardReviewAttempts.CountAsync());
    Assert.Single(await _context.FlashcardReviews.ToListAsync());
}

[Fact]
public async Task GetHistoryDetailAsync_ForForeignUser_ReturnsNull()
{
    Assert.Null(await _service.GetHistoryDetailAsync(_attemptId, Guid.NewGuid()));
}
```

- [ ] **Step 2: Run and confirm failure**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --no-restore --filter FullyQualifiedName~FlashcardReviewHistoryTests`

Expected: FAIL because attempts and history methods are absent.

- [ ] **Step 3: Implement attempt recording and endpoints**

Capture before-values, run `ApplySm2`, add the attempt before `SaveChangesAsync`, then update `XpEarned` and save again only if XP succeeds. Add paged filters and eager-load `Flashcard.Document.Subject` for detail. Controller routes are exactly:

```text
GET /api/FlashcardReview/history
GET /api/FlashcardReview/history/{attemptId}
```

- [ ] **Step 4: Run Flashcard review tests**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --no-restore --filter "FullyQualifiedName~FlashcardReviewHistoryTests|FullyQualifiedName~FlashcardReviewService"`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add AIStudyHub.Business/Interfaces/Services/IFlashcardReviewService.cs AIStudyHub.Business/Services/FlashcardReviewService.cs AIStudyHub.API/Controllers/FlashcardReviewController.cs AIStudyHub.Tests/Services/FlashcardReviewHistoryTests.cs
git commit -m "feat: add flashcard review history"
```

### Task 6: Delete complete Flashcard decks safely

**Files:**
- Modify: `AIStudyHub.Business/Interfaces/Services/IFlashcardService.cs`
- Modify: `AIStudyHub.Business/Services/ModuleServices.cs`
- Modify: `AIStudyHub.API/Controllers/FlashcardController.cs`
- Create: `AIStudyHub.Tests/Services/FlashcardDeckDeletionTests.cs`

**Interfaces:**
- Produces: `DeleteDeckAsync(Guid documentId, Guid ownerUserId, CancellationToken ct)`
- Produces: `DELETE /api/Flashcard/document/{documentId}`

- [ ] **Step 1: Write failing deletion tests**

```csharp
[Fact]
public async Task DeleteDeckAsync_RemovesCardsSchedulesAndAttemptsOnly()
{
    await _service.DeleteDeckAsync(_documentId, _ownerId);
    Assert.Empty(_context.Flashcards.Where(x => x.DocumentId == _documentId));
    Assert.Empty(_context.FlashcardReviews);
    Assert.Empty(_context.FlashcardReviewAttempts);
    Assert.NotNull(await _context.Documents.FindAsync(_documentId));
    Assert.NotEmpty(_context.Quizzes.Where(x => x.DocumentId == _documentId));
}
```

- [ ] **Step 2: Run and confirm failure**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --no-restore --filter FullyQualifiedName~FlashcardDeckDeletionTests`

Expected: FAIL because bulk deletion does not exist.

- [ ] **Step 3: Implement owner-filtered transactional deletion**

Query the Document by both ID and owner. Collect card IDs, remove attempts, schedules, then cards, and save once. Update single-card deletion to remove the same dependent rows. An owned empty deck completes without error.

- [ ] **Step 4: Run deletion tests**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --no-restore --filter FullyQualifiedName~FlashcardDeckDeletionTests`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add AIStudyHub.Business/Interfaces/Services/IFlashcardService.cs AIStudyHub.Business/Services/ModuleServices.cs AIStudyHub.API/Controllers/FlashcardController.cs AIStudyHub.Tests/Services/FlashcardDeckDeletionTests.cs
git commit -m "feat: delete complete flashcard decks"
```

### Task 7: Add learning-history migration and verify

**Files:**
- Create: `AIStudyHub.Data/Migrations/20260729110000_AddLearningHistoryDetails.cs`
- Create: `AIStudyHub.Data/Migrations/20260729110000_AddLearningHistoryDetails.Designer.cs`
- Modify: `AIStudyHub.Data/Migrations/ApplicationDbContextModelSnapshot.cs`

- [ ] **Step 1: Generate migration**

Run: `dotnet ef migrations add AddLearningHistoryDetails --project AIStudyHub.Data/AIStudyHub.Data.csproj --startup-project AIStudyHub.API/AIStudyHub.API.csproj`

Expected: adds `duration_seconds` and the Flashcard review-attempt table with indexes/FKs.

- [ ] **Step 2: Run full tests**

Run: `dotnet test AIStudyHub.slnx --no-restore`

Expected: PASS.

- [ ] **Step 3: Commit**

```powershell
git add AIStudyHub.Data/Migrations
git commit -m "db: add quiz and flashcard history details"
```
