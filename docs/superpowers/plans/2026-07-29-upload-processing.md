# Upload and Durable Document Processing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enforce a 5 MiB file limit and return from upload after durable acceptance without waiting for chunking or embedding.

**Architecture:** `DocumentStorageOptions` owns file constraints. Upload commits a Document and database-backed `DocumentProcessingJob`; a polling worker claims and processes jobs independently with bounded retries.

**Tech Stack:** ASP.NET Core 8, EF Core 8, SQL Server, BackgroundService, xUnit

## Global Constraints

- Exact file-content maximum: 5,242,880 bytes.
- Upload returns `202 Accepted` after durable database acceptance.
- Existing migration files remain unchanged.
- Chunking and embedding failures update status and never retroactively fail upload.
- Large-scale load tuning remains outside this plan.

---

### Task 1: Consolidate and validate the 5 MiB limit

**Files:**
- Modify: `AIStudyHub.Business/Options/DocumentStorageOptions.cs`
- Modify: `AIStudyHub.Business/Options/RagOptions.cs`
- Create: `AIStudyHub.Business/Services/DocumentUploadPolicy.cs`
- Modify: `AIStudyHub.API/appsettings.json`
- Modify: `AIStudyHub.API/Program.cs`
- Modify: `AIStudyHub.API/Controllers/DocumentController.cs`
- Create: `AIStudyHub.Tests/Controllers/DocumentUploadLimitTests.cs`

**Interfaces:**
- Produces: `DocumentStorageOptions.MaxFileSizeBytes = 5_242_880`
- Produces: oversized file → HTTP 413

- [ ] **Step 1: Add boundary tests**

```csharp
[Theory]
[InlineData(5_242_880, false)]
[InlineData(5_242_881, true)]
public void IsFileTooLarge_UsesExactFiveMiBBoundary(long length, bool expected)
{
    Assert.Equal(expected, DocumentUploadPolicy.IsFileTooLarge(length, 5_242_880));
}
```

- [ ] **Step 2: Run and confirm failure**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --no-restore --filter FullyQualifiedName~DocumentUploadLimitTests`

Expected: FAIL because `DocumentUploadPolicy` does not exist.

- [ ] **Step 3: Add a focused upload policy and single option source**

Create `AIStudyHub.Business/Services/DocumentUploadPolicy.cs`:

```csharp
public static class DocumentUploadPolicy
{
    public const long FiveMiB = 5L * 1024 * 1024;
    public static bool IsFileTooLarge(long fileLength, long configuredMaximum) =>
        fileLength > configuredMaximum;
}
```

Set the option default and JSON value to `5242880`, remove `MaxFileSizeBytes` from `RagOptions`, inject only `DocumentStorageOptions`, and return:

```csharp
return StatusCode(StatusCodes.Status413PayloadTooLarge,
    $"File exceeds maximum allowed size of {_storageOptions.MaxFileSizeBytes} bytes.");
```

Configure multipart body allowance to 6 MiB so protocol overhead is accepted while the application enforces exactly 5 MiB of file content.

- [ ] **Step 4: Run upload-limit tests**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --no-restore --filter FullyQualifiedName~DocumentUploadLimitTests`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add AIStudyHub.Business/Options/DocumentStorageOptions.cs AIStudyHub.Business/Options/RagOptions.cs AIStudyHub.Business/Services/DocumentUploadPolicy.cs AIStudyHub.API/appsettings.json AIStudyHub.API/Program.cs AIStudyHub.API/Controllers/DocumentController.cs AIStudyHub.Tests/Controllers/DocumentUploadLimitTests.cs
git commit -m "fix: enforce five mibibyte document limit"
```

### Task 2: Add durable processing-job persistence

**Files:**
- Create: `AIStudyHub.Data/Enums/DocumentProcessingJobStatus.cs`
- Create: `AIStudyHub.Data/Entities/DocumentProcessingJob.cs`
- Modify: `AIStudyHub.Data/Entities/Document.cs`
- Modify: `AIStudyHub.Data/Configurations/EntityConfigurations.cs`
- Modify: `AIStudyHub.Data/ApplicationDbContext.cs`
- Modify: `AIStudyHub.Data/Interfaces/IUnitOfWork.cs`
- Modify: `AIStudyHub.Data/Repositories/UnitOfWork.cs`
- Create: `AIStudyHub.Tests/Services/DocumentProcessingJobModelTests.cs`

**Interfaces:**
- Produces: one `DocumentProcessingJob` per Document
- Produces: `Queued`, `Processing`, `Completed`, `Failed`

- [ ] **Step 1: Write a failing model test**

```csharp
[Fact]
public void ProcessingJob_HasUniqueDocumentAndClaimIndex()
{
    var options = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlite("DataSource=:memory:")
        .Options;
    using var context = new ApplicationDbContext(options);
    var entity = context.Model.FindEntityType(typeof(DocumentProcessingJob))!;
    Assert.Contains(entity.GetIndexes(), index =>
        index.IsUnique && index.Properties.Single().Name == nameof(DocumentProcessingJob.DocumentId));
    Assert.Contains(entity.GetIndexes(), index =>
        index.Properties.Select(p => p.Name).SequenceEqual(
            new[] { nameof(DocumentProcessingJob.Status), nameof(DocumentProcessingJob.NextAttemptAt) }));
}
```

- [ ] **Step 2: Run and confirm failure**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --no-restore --filter FullyQualifiedName~DocumentProcessingJobModelTests`

Expected: FAIL because the job entity does not exist.

- [ ] **Step 3: Implement the entity**

```csharp
public sealed class DocumentProcessingJob : BaseEntity
{
    public Guid DocumentId { get; set; }
    public DocumentProcessingJobStatus Status { get; set; } = DocumentProcessingJobStatus.Queued;
    public int AttemptCount { get; set; }
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    public Guid? ClaimId { get; set; }
    public DateTime? ClaimedAt { get; set; }
    public string? LastError { get; set; }
    public Document Document { get; set; } = null!;
}
```

Configure a unique Document ID, status/next-attempt index, cascade deletion from Document, enum-to-string conversion, and repository/DbSet access.

- [ ] **Step 4: Run the model test**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --no-restore --filter FullyQualifiedName~DocumentProcessingJobModelTests`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add AIStudyHub.Data AIStudyHub.Tests/Services/DocumentProcessingJobModelTests.cs
git commit -m "feat: persist document processing jobs"
```

### Task 3: Claim jobs atomically

**Files:**
- Create: `AIStudyHub.Business/Interfaces/Services/IDocumentProcessingJobService.cs`
- Create: `AIStudyHub.Business/Services/DocumentProcessingJobService.cs`
- Modify: `AIStudyHub.Business/Services/BusinessServiceExtensions.cs`
- Create: `AIStudyHub.Tests/Services/DocumentProcessingJobServiceTests.cs`

**Interfaces:**
- Produces: `Task<DocumentProcessingJob?> ClaimNextAsync(Guid claimId, DateTime utcNow, CancellationToken ct)`
- Produces: `Task CompleteAsync(Guid jobId, Guid claimId, CancellationToken ct)`
- Produces: `Task FailAsync(Guid jobId, Guid claimId, string error, DateTime utcNow, CancellationToken ct)`

- [ ] **Step 1: Write failing claim/retry tests**

```csharp
[Fact]
public async Task ClaimNextAsync_AllowsOnlyOneClaim()
{
    var first = await _service.ClaimNextAsync(Guid.NewGuid(), _clock, default);
    var second = await _service.ClaimNextAsync(Guid.NewGuid(), _clock, default);
    Assert.NotNull(first);
    Assert.Null(second);
}

[Fact]
public async Task FailAsync_SchedulesExponentialRetry()
{
    await _service.FailAsync(_jobId, _claimId, "embedding failed", _clock, default);
    var job = await _context.DocumentProcessingJobs.FindAsync(_jobId);
    Assert.Equal(1, job!.AttemptCount);
    Assert.Equal(_clock.AddSeconds(2), job.NextAttemptAt);
}
```

- [ ] **Step 2: Run and confirm failure**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --no-restore --filter FullyQualifiedName~DocumentProcessingJobServiceTests`

Expected: FAIL because the service does not exist.

- [ ] **Step 3: Implement claim ownership and retry rules**

Use an EF transaction with serializable isolation for the initial implementation. Eligible jobs are queued jobs whose `NextAttemptAt <= utcNow`, plus processing jobs whose `ClaimedAt` is older than five minutes. Backoff is `2^AttemptCount` seconds capped at five minutes; after three failed attempts set `Failed`.

- [ ] **Step 4: Run job-service tests**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --no-restore --filter FullyQualifiedName~DocumentProcessingJobServiceTests`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add AIStudyHub.Business/Interfaces/Services/IDocumentProcessingJobService.cs AIStudyHub.Business/Services/DocumentProcessingJobService.cs AIStudyHub.Business/Services/BusinessServiceExtensions.cs AIStudyHub.Tests/Services/DocumentProcessingJobServiceTests.cs
git commit -m "feat: claim and retry document jobs"
```

### Task 4: Make upload persist a job and return immediately

**Files:**
- Modify: `AIStudyHub.API/Controllers/DocumentController.cs`
- Modify: `AIStudyHub.Business/DTOs/Rag/UploadDocumentResponseDto.cs`
- Create: `AIStudyHub.Tests/Controllers/DocumentUploadAcceptanceTests.cs`

**Interfaces:**
- Consumes: `DocumentProcessingJob`
- Produces: accepted Document and queued job in one SaveChanges call

- [ ] **Step 1: Add an acceptance test with a processing service that throws if invoked**

```csharp
[Fact]
public async Task Upload_ReturnsAcceptedWithoutCallingProcessor()
{
    var result = await _controller.UploadDocumentFile(ValidRequest(), default);
    Assert.IsType<AcceptedResult>(result.Result);
    Assert.Single(_context.DocumentProcessingJobs);
    _processor.VerifyNoOtherCalls();
}
```

- [ ] **Step 2: Run and confirm failure**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --no-restore --filter FullyQualifiedName~DocumentUploadAcceptanceTests`

Expected: FAIL because upload writes to the in-memory queue and creates no job.

- [ ] **Step 3: Replace queue enqueue with job persistence**

Create the queued job before the existing `SaveChangesAsync` and remove `IDocumentProcessingQueue` from the controller constructor. Return `Accepted` immediately after the database commit.

- [ ] **Step 4: Run acceptance tests**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --no-restore --filter FullyQualifiedName~DocumentUploadAcceptanceTests`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add AIStudyHub.API/Controllers/DocumentController.cs AIStudyHub.Business/DTOs/Rag/UploadDocumentResponseDto.cs AIStudyHub.Tests/Controllers/DocumentUploadAcceptanceTests.cs
git commit -m "refactor: durably accept uploads before processing"
```

### Task 5: Convert the worker and reprocess flow to database jobs

**Files:**
- Modify: `AIStudyHub.Business/Workers/DocumentBackgroundProcessor.cs`
- Modify: `AIStudyHub.API/Controllers/DocumentController.cs`
- Modify: `AIStudyHub.API/Controllers/AdminController.cs`
- Modify: `AIStudyHub.Business/Workers/DocumentReindexWorker.cs`
- Modify: `AIStudyHub.Business/Services/BusinessServiceExtensions.cs`
- Delete: `AIStudyHub.Business/Services/DocumentProcessingQueue.cs`
- Create: `AIStudyHub.Tests/Services/DocumentBackgroundJobTests.cs`

**Interfaces:**
- Consumes: `IDocumentProcessingJobService`
- Produces: polling worker with success/failure transitions

- [ ] **Step 1: Write worker transition tests**

```csharp
[Fact]
public async Task RunOnceAsync_WhenProcessingSucceeds_CompletesJobAndDocument()
{
    await _worker.RunOnceAsync(default);
    Assert.Equal(DocumentStatus.Done, ReloadDocument().Status);
    Assert.Equal(DocumentProcessingJobStatus.Completed, ReloadJob().Status);
}

[Fact]
public async Task RunOnceAsync_WhenEmbeddingFails_SchedulesRetry()
{
    _embedding.Setup(x => x.GenerateEmbeddingsAsync(It.IsAny<List<string>>()))
        .ThrowsAsync(new InvalidOperationException("embedding failed"));
    await _worker.RunOnceAsync(default);
    Assert.Equal(DocumentStatus.Failed, ReloadDocument().Status);
    Assert.Equal(DocumentProcessingJobStatus.Queued, ReloadJob().Status);
}
```

- [ ] **Step 2: Run and confirm failure**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --no-restore --filter FullyQualifiedName~DocumentBackgroundJobTests`

Expected: FAIL because the worker consumes the channel.

- [ ] **Step 3: Implement polling**

Expose an internal `RunOnceAsync` for tests. The hosted loop calls it, delays one second when no work is available, and passes the claimed job's Document/file metadata into the existing processing pipeline. Reprocess resets or creates the job with `Queued`, zero attempts, and `NextAttemptAt = DateTime.UtcNow`.

- [ ] **Step 4: Run worker and document tests**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --no-restore --filter "FullyQualifiedName~DocumentBackgroundJobTests|FullyQualifiedName~DocumentProcessing"`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add AIStudyHub.Business/Workers/DocumentBackgroundProcessor.cs AIStudyHub.API/Controllers/DocumentController.cs AIStudyHub.API/Controllers/AdminController.cs AIStudyHub.Business/Workers/DocumentReindexWorker.cs AIStudyHub.Business/Services/BusinessServiceExtensions.cs AIStudyHub.Business/Services/DocumentProcessingQueue.cs AIStudyHub.Tests/Services/DocumentBackgroundJobTests.cs
git commit -m "refactor: process documents from durable jobs"
```

### Task 6: Add migration and full verification

**Files:**
- Create: `AIStudyHub.Data/Migrations/20260729100000_AddDocumentProcessingJobs.cs`
- Create: `AIStudyHub.Data/Migrations/20260729100000_AddDocumentProcessingJobs.Designer.cs`
- Modify: `AIStudyHub.Data/Migrations/ApplicationDbContextModelSnapshot.cs`

- [ ] **Step 1: Generate migration**

Run: `dotnet ef migrations add AddDocumentProcessingJobs --project AIStudyHub.Data/AIStudyHub.Data.csproj --startup-project AIStudyHub.API/AIStudyHub.API.csproj`

Expected: migration creates the job table, indexes, and Document FK.

- [ ] **Step 2: Run full tests**

Run: `dotnet test AIStudyHub.slnx --no-restore`

Expected: PASS.

- [ ] **Step 3: Verify active queue references are gone**

Run: `rg -n "IDocumentProcessingQueue|DocumentProcessingQueue" AIStudyHub.API AIStudyHub.Business AIStudyHub.Data AIStudyHub.Tests`

Expected: no matches.

- [ ] **Step 4: Commit migration**

```powershell
git add AIStudyHub.Data/Migrations
git commit -m "db: add durable document processing jobs"
```
