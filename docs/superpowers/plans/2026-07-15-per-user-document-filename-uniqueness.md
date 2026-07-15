# Per-user Document Filename Uniqueness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give every active document an owner-scoped, case-insensitively unique display filename with deterministic numeric suffixes on upload and restore.

**Architecture:** Keep allocation in the existing `DocumentService` and expose one narrow method through `IDocumentService`. Upload and restore reuse it, while a filtered SQL Server unique index protects concurrent writes. Citation identity remains `DocumentId`.

**Tech Stack:** ASP.NET Core 8, C# 12, Entity Framework Core 8, SQL Server, xUnit, Moq, SQLite.

## Global Constraints

- Uniqueness is per user and only for `DocumentLifecycleStatus.Active` rows.
- Comparison is case-insensitive; preserve `Title`, `FileLink`, physical file naming, and `DocumentId`.
- Trashed rows release names; restore takes the smallest available suffix.
- Do not add a new service/project or broadly refactor existing large files.
- Preserve unrelated working-tree citation changes.

---

### Task 1: Filename allocator

**Files:**
- Modify: `AIStudyHub.Business/Interfaces/Services/IDocumentService.cs`
- Modify: `AIStudyHub.Business/Services/ModuleServices.cs`
- Test: `AIStudyHub.Tests/Services/DocumentServiceTests.cs`

**Interfaces:**
- Consumes: `IUnitOfWork.Documents.Query()`.
- Produces: `Task<string> GetAvailableFileNameAsync(Guid userId, string fileName, Guid? excludeDocumentId = null, CancellationToken cancellationToken = default)`.

- [ ] **Step 1: Write failing tests**

Seed active and trashed documents, then assert exact outputs:

```csharp
Assert.Equal("abc.pdf", await _service.GetAvailableFileNameAsync(userId, "abc.pdf"));
Assert.Equal("abc (2).pdf", await _service.GetAvailableFileNameAsync(userId, "abc.pdf"));
Assert.Equal("ABC (1).pdf", await _service.GetAvailableFileNameAsync(userId, "ABC.pdf"));
Assert.Equal("archive.tar (1).gz", await _service.GetAvailableFileNameAsync(userId, "archive.tar.gz"));
Assert.Equal("README (1)", await _service.GetAvailableFileNameAsync(userId, "README"));
Assert.Equal("abc (1) (1).pdf", await _service.GetAvailableFileNameAsync(userId, "abc (1).pdf"));
```

Also cover another owner, trashed rows, `excludeDocumentId`, path stripping, and the 255-character limit.

- [ ] **Step 2: Verify tests fail**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --filter "FullyQualifiedName~DocumentServiceTests"`

Expected: build failure because the method is absent.

- [ ] **Step 3: Implement the contract and allocator**

Add the exact signature above. Normalize with `Path.GetFileName`; reject empty input with `ArgumentException`. Query active owner filenames excluding the optional ID into `HashSet<string>(StringComparer.OrdinalIgnoreCase)`. Preserve the literal requested stem and extension, append ` (n)` from 1 upward, and truncate only the stem enough to keep the result within 255 characters.

- [ ] **Step 4: Verify tests pass**

Run the Task 1 test command. Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add AIStudyHub.Business/Interfaces/Services/IDocumentService.cs AIStudyHub.Business/Services/ModuleServices.cs AIStudyHub.Tests/Services/DocumentServiceTests.cs
git commit -m "feat: allocate unique document filenames per user"
```

### Task 2: Restore and upload integration

**Files:**
- Modify: `AIStudyHub.Business/Services/ModuleServices.cs`
- Modify: `AIStudyHub.API/Controllers/DocumentController.cs`
- Test: `AIStudyHub.Tests/Services/DocumentServiceTrashBinTests.cs`

**Interfaces:**
- Consumes: `GetAvailableFileNameAsync` from Task 1.
- Produces: restore and upload flows that persist and enqueue the allocated filename.

- [ ] **Step 1: Write failing restore tests**

Create a trashed `abc.pdf` plus active `abc.pdf` for the same owner; after restore assert `abc (1).pdf`. Also assert a free former name stays unchanged and another owner's name does not collide.

- [ ] **Step 2: Verify collision test fails**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --filter "FullyQualifiedName~DocumentServiceTrashBinTests"`

Expected: collision assertion fails with `abc.pdf`.

- [ ] **Step 3: Allocate during restore**

Before setting the lifecycle active:

```csharp
document.FileName = await GetAvailableFileNameAsync(
    userId, document.FileName ?? "document", document.Id, cancellationToken);
```

Keep authorization, purged-state checks, timestamps, and save behavior unchanged.

- [ ] **Step 4: Allocate during upload with bounded collision retry**

In `DocumentController.Upload`, request `allocatedFileName` after validation. Assign it to both `Document.FileName` and `DocumentProcessRequest.FileName`. Keep the original basename for GUID-backed physical storage. Wrap the document save in a maximum of three attempts. On SQL Server errors 2601/2627 for `UX_Document_UserId_FileName_Active`, recalculate the filename, update the tracked document, and retry; rethrow every other `DbUpdateException`. After three collisions, throw `InvalidOperationException("Could not allocate a unique document filename after 3 attempts.")`, which the controller maps to HTTP 409. Apply the same three-attempt policy around the state transition in `RestoreAsync`.

- [ ] **Step 5: Verify affected tests and commit**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --filter "FullyQualifiedName~DocumentServiceTrashBinTests|FullyQualifiedName~DocumentServiceTests|FullyQualifiedName~AIChatServiceTests"`

Expected: PASS.

```powershell
git add AIStudyHub.Business/Services/ModuleServices.cs AIStudyHub.API/Controllers/DocumentController.cs AIStudyHub.Tests/Services/DocumentServiceTrashBinTests.cs
git commit -m "feat: resolve filename collisions on upload and restore"
```

### Task 3: Database enforcement and migration

**Files:**
- Modify: `AIStudyHub.Data/Configurations/EntityConfigurations.cs`
- Create (generated): `AIStudyHub.Data/Migrations/*_AddActiveDocumentFileNameUniqueness.cs`
- Create (generated): `AIStudyHub.Data/Migrations/*_AddActiveDocumentFileNameUniqueness.Designer.cs`
- Modify: `AIStudyHub.Data/Migrations/ApplicationDbContextModelSnapshot.cs`
- Test: `AIStudyHub.Tests/Services/DocumentServiceTests.cs`

**Interfaces:**
- Consumes: filenames from Tasks 1–2.
- Produces: `UX_Document_UserId_FileName_Active`.

- [ ] **Step 1: Write a failing EF metadata test**

Assert `Document` has a unique composite index over `UserId` and `FileName` whose filter includes active lifecycle and non-null filename conditions.

- [ ] **Step 2: Verify the metadata test fails**

Run the Task 1 test command. Expected: FAIL because the index is absent.

- [ ] **Step 3: Configure the index**

```csharp
builder.HasIndex(x => new { x.UserId, x.FileName })
    .HasDatabaseName("UX_Document_UserId_FileName_Active")
    .IsUnique()
    .HasFilter("[lifecycle_status] = 'Active' AND [file_name] IS NOT NULL");
```

Keep the database's existing case-insensitive SQL Server collation. Before applying the migration, run `SELECT CONVERT(varchar(128), DATABASEPROPERTYEX(DB_NAME(), 'Collation'));`; abort deployment if it contains `_CS_`, because the application allocator requires a `_CI_` collation.

- [ ] **Step 4: Generate migration**

Run: `dotnet ef migrations add AddActiveDocumentFileNameUniqueness --project AIStudyHub.Data --startup-project AIStudyHub.API`

Expected: migration, designer, and snapshot generated.

- [ ] **Step 5: Protect legacy data**

Before `CreateIndex`, add SQL that groups non-null active names by `u_id` plus case-insensitive `file_name`, orders by `create_at, doc_id`, lets the earliest keep its name, and assigns later rows the smallest available suffix within 255 characters. `Down` drops only the index because historical renames are not reversible.

- [ ] **Step 6: Verify migration and commit**

Run:

```powershell
dotnet ef migrations script --project AIStudyHub.Data --startup-project AIStudyHub.API --idempotent
dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --filter "FullyQualifiedName~DocumentServiceTests"
```

Expected: cleanup SQL precedes the unique index and tests PASS.

```powershell
git add AIStudyHub.Data/Configurations/EntityConfigurations.cs AIStudyHub.Data/Migrations AIStudyHub.Tests/Services/DocumentServiceTests.cs
git commit -m "feat: enforce active document filename uniqueness"
```

### Task 4: Citation regression and full verification

**Files:**
- Test: `AIStudyHub.Tests/Services/AIChatServiceTests.cs`
- Verify existing citation edits without overwriting them.

**Interfaces:**
- Consumes: `CitationInfo.DocumentId` and `ChatCitationDto`.
- Produces: regression proof that filename is display-only.

- [ ] **Step 1: Write citation mapping test**

Return two mocked citations with different `DocumentId` values and assert the response preserves ID, source, page, relevance, match type, and 300-character snippet truncation.

- [ ] **Step 2: Run citation tests**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --filter "FullyQualifiedName~AIChatServiceTests"`

Expected: PASS; fix only regressions exposed by existing citation edits.

- [ ] **Step 3: Run complete verification**

```powershell
dotnet build AIStudyHub.slnx --no-restore
dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --no-build
git diff --check
```

Expected: build and all tests succeed; diff check is clean.

- [ ] **Step 4: Commit the regression test if changed**

```powershell
git add AIStudyHub.Tests/Services/AIChatServiceTests.cs
git commit -m "test: cover document identity in chat citations"
```

- [ ] **Step 5: Final scope audit**

Run `git status --short` and inspect the final diff. Report the migration name and distinguish pre-existing citation changes from filename-uniqueness changes.
