# Register and Student-Owned Subject CRUD Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove date of birth only from registration and replace hard-coded global Subjects with private student-owned CRUD plus a coordinated demo learning-data reset.

**Architecture:** Keep the existing API/Business/Data projects. Registration changes remain contract-only. Subject ownership is enforced in explicit Business service queries using the JWT User ID, while a new EF Core migration removes incompatible demo learning data and changes Subject uniqueness from global to per owner.

**Tech Stack:** ASP.NET Core 8, C# 12, FluentValidation 12, AutoMapper 16, EF Core 8, SQL Server.

## Global Constraints

- Do not recreate `AIStudyHub.Tests` or create any test project/file.
- Do not run unit tests, integration tests, or smoke tests.
- Use `dotnet build AIStudyHub.slnx --no-restore` for agent verification.
- The repository owner performs all functional verification manually.
- New migrations are allowed; every existing migration is immutable.
- Never run `dotnet ef migrations remove` against a committed migration.
- Do not apply migrations, delete SQL data, clear Qdrant, or delete uploaded files without separate explicit authorization.
- Preserve the three-project solution and do not add another project.

---

## File Structure

### Registration contract

- Modify `AIStudyHub.Business/DTOs/Authentication/AuthDtos.cs` to remove `DateOfBirth` from `RegisterRequestDto`.
- Modify `AIStudyHub.Business/Validators/Authentication/AuthValidators.cs` to remove only registration date validation.
- Modify `AIStudyHub.Business/Services/AuthService.cs` so registration creates a User without a date argument.
- Keep `AIStudyHub.Data/Entities/User.cs`, User DTOs, User validators, mappings, services, and `Users.dob`.

### Subject ownership

- Modify `AIStudyHub.Data/Entities/Subject.cs` to add `OwnerUserId` and `OwnerUser`.
- Modify `AIStudyHub.Data/Entities/User.cs` to add `Subjects`.
- Modify `AIStudyHub.Data/Configurations/EntityConfigurations.cs` to remove Subject seed data and configure ownership/indexes.
- Modify `AIStudyHub.Business/Interfaces/Services/ISubjectService.cs` to replace generic CRUD with owner-explicit methods.
- Modify `AIStudyHub.Business/Services/ModuleServices.cs` to implement owner-scoped Subject operations.
- Modify `AIStudyHub.API/Controllers/SubjectController.cs` to derive the owner from JWT and remove Admin-only writes.
- Modify `AIStudyHub.Business/Mappings/ApplicationMappingProfile.cs` so request mapping never assigns ownership.
- Modify `AIStudyHub.Business/Services/ModuleServices.cs` Document creation guard to validate Subject ownership.
- Modify `AIStudyHub.Business/Workers/UnverifiedAccountCleanupService.cs` only if required by the generated foreign-key behavior.

### Migration and documentation

- Create generated migration files matching `AIStudyHub.Data/Migrations/*_OwnSubjectsAndCleanDemoLearningData.cs`.
- Modify generated `AIStudyHub.Data/Migrations/ApplicationDbContextModelSnapshot.cs`.
- Modify `README.md`, `AGENT.md`, `ARCHITECTURE.md`, and `docs/FRONTEND_GUIDE.md` for the registration and Subject contracts.

---

### Task 1: Remove DateOfBirth from Registration Only

**Files:**
- Modify: `AIStudyHub.Business/DTOs/Authentication/AuthDtos.cs`
- Modify: `AIStudyHub.Business/Validators/Authentication/AuthValidators.cs`
- Modify: `AIStudyHub.Business/Services/AuthService.cs`

**Interfaces:**
- Produces: `RegisterRequestDto(string FullName, string Email, string Password)`
- Preserves: all profile/admin DTOs and `User.DateOfBirth`

- [ ] **Step 1: Change the registration DTO**

Replace the registration record with:

```csharp
public sealed record RegisterRequestDto(
    string FullName,
    string Email,
    string Password);
```

- [ ] **Step 2: Remove registration-only date validation**

Delete the `today`, `minDate`, `maxDate`, and `RuleFor(x => x.DateOfBirth)` block from `RegisterRequestDtoValidator`. Do not change `UserValidators.cs`.

- [ ] **Step 3: Remove the date argument from registration**

Change the registration call to:

```csharp
var user = await BuildStudentUserAsync(
    normalizedEmail,
    request.FullName,
    cancellationToken);
```

Update the Google-sign-in user-creation call to the same three-argument shape;
it currently passes a literal `null` date and must compile after the helper
changes.

Change the helper signature to:

```csharp
private async Task<User> BuildStudentUserAsync(
    string normalizedEmail,
    string fullName,
    CancellationToken cancellationToken)
```

Remove `DateOfBirth = dateOfBirth` from the helper initializer. Leave every other Auth behavior unchanged.

- [ ] **Step 4: Verify scope and compilation**

Run:

```powershell
rg -n "DateOfBirth" AIStudyHub.Business/DTOs/Authentication/AuthDtos.cs AIStudyHub.Business/Validators/Authentication/AuthValidators.cs AIStudyHub.Business/Services/AuthService.cs
dotnet build AIStudyHub.slnx --no-restore
```

Expected:

- No registration-path `DateOfBirth` reference remains.
- Profile/admin references may remain.
- Build exits 0.

- [ ] **Step 5: Commit the registration slice**

```powershell
git add AIStudyHub.Business/DTOs/Authentication/AuthDtos.cs AIStudyHub.Business/Validators/Authentication/AuthValidators.cs AIStudyHub.Business/Services/AuthService.cs
git diff --cached --check
git commit -m "refactor: remove date of birth from registration"
```

---

### Task 2: Add Subject Ownership to the Domain Model

**Files:**
- Modify: `AIStudyHub.Data/Entities/Subject.cs`
- Modify: `AIStudyHub.Data/Entities/User.cs`
- Modify: `AIStudyHub.Data/Configurations/EntityConfigurations.cs`
- Modify: `AIStudyHub.Business/Mappings/ApplicationMappingProfile.cs`

**Interfaces:**
- Produces: `Subject.OwnerUserId`, `Subject.OwnerUser`, `User.Subjects`
- Produces index: `(OwnerUserId, SubjectCode)` unique
- Preserves: `SubjectResponseDto` shape without exposing owner ID

- [ ] **Step 1: Add ownership properties**

Add to `Subject`:

```csharp
public Guid OwnerUserId { get; set; }
public User OwnerUser { get; set; } = null!;
```

Add to `User`:

```csharp
public ICollection<Subject> Subjects { get; set; } = new List<Subject>();
```

- [ ] **Step 2: Replace global Subject configuration**

In `SubjectConfiguration`:

```csharp
builder.Property(x => x.OwnerUserId)
    .HasColumnName("owner_user_id")
    .IsRequired();

builder.HasIndex(x => new { x.OwnerUserId, x.SubjectCode })
    .IsUnique();

builder.HasOne(x => x.OwnerUser)
    .WithMany(x => x.Subjects)
    .HasForeignKey(x => x.OwnerUserId)
    .OnDelete(DeleteBehavior.Cascade);
```

Delete the global `HasIndex(x => x.SubjectCode).IsUnique()` and the complete Subject `HasData(...)` block. Keep the Document-to-Subject relationship restrictive.

- [ ] **Step 3: Prevent AutoMapper from assigning ownership**

Configure request mappings:

```csharp
CreateMap<CreateSubjectRequestDto, Subject>()
    .ForMember(destination => destination.OwnerUserId, option => option.Ignore())
    .ForMember(destination => destination.OwnerUser, option => option.Ignore());

CreateMap<UpdateSubjectRequestDto, Subject>()
    .ForMember(destination => destination.OwnerUserId, option => option.Ignore())
    .ForMember(destination => destination.OwnerUser, option => option.Ignore());
```

- [ ] **Step 4: Verify the model edits compile before migration generation**

Run:

```powershell
dotnet build AIStudyHub.slnx --no-restore
```

Expected: build exits 0; a pending EF model change is expected.

- [ ] **Step 5: Commit the domain-model portion**

```powershell
git add AIStudyHub.Data/Entities/Subject.cs AIStudyHub.Data/Entities/User.cs AIStudyHub.Data/Configurations/EntityConfigurations.cs AIStudyHub.Business/Mappings/ApplicationMappingProfile.cs
git diff --cached --check
git commit -m "refactor: model student-owned subjects"
```

---

### Task 3: Replace Generic Subject CRUD with Owner-Scoped Operations

**Files:**
- Modify: `AIStudyHub.Business/Interfaces/Services/ISubjectService.cs`
- Modify: `AIStudyHub.Business/Services/ModuleServices.cs`
- Modify: `AIStudyHub.API/Controllers/SubjectController.cs`

**Interfaces:**
- Produces:

```csharp
Task<PagedResultDto<SubjectResponseDto>> GetMineAsync(
    Guid ownerUserId,
    PaginationParams pagination,
    CancellationToken cancellationToken = default);

Task<SubjectResponseDto?> GetOwnedByIdAsync(
    Guid ownerUserId,
    Guid subjectId,
    CancellationToken cancellationToken = default);

Task<SubjectResponseDto> CreateForUserAsync(
    Guid ownerUserId,
    CreateSubjectRequestDto request,
    CancellationToken cancellationToken = default);

Task<SubjectResponseDto> UpdateOwnedAsync(
    Guid ownerUserId,
    Guid subjectId,
    UpdateSubjectRequestDto request,
    CancellationToken cancellationToken = default);

Task DeleteOwnedAsync(
    Guid ownerUserId,
    Guid subjectId,
    CancellationToken cancellationToken = default);

Task<bool> ExistsForOwnerAsync(
    Guid ownerUserId,
    Guid subjectId,
    CancellationToken cancellationToken = default);
```

- [ ] **Step 1: Replace the Subject service contract**

Remove `: ICrudService<...>` and define the six signatures above. Add imports for `PagedResultDto` and `PaginationParams`.

- [ ] **Step 2: Implement owner-scoped reads**

Use:

```csharp
var query = _unitOfWork.Subjects.Query()
    .Where(subject => subject.OwnerUserId == ownerUserId)
    .AsNoTracking();
```

Apply search, ordering, offset, and limit to this query. Detail must use:

```csharp
.FirstOrDefaultAsync(
    subject => subject.Id == subjectId
        && subject.OwnerUserId == ownerUserId,
    cancellationToken);
```

- [ ] **Step 3: Implement normalized create/update**

Normalize code once:

```csharp
var normalizedCode = request.SubjectCode.Trim().ToUpperInvariant();
```

Check conflict using both owner and normalized code. On create:

```csharp
var subject = _mapper.Map<Subject>(request);
subject.OwnerUserId = ownerUserId;
subject.SubjectCode = normalizedCode;
subject.SubjectName = request.SubjectName.Trim();
```

On update, load by ID and owner, preserve `OwnerUserId`, and assign only code/name/description.

- [ ] **Step 4: Implement referenced-Subject deletion guard**

Before removing:

```csharp
var isReferenced = await _unitOfWork.Documents.Query()
    .AnyAsync(document => document.SubjectId == subjectId, cancellationToken);

if (isReferenced)
{
    throw new InvalidOperationException(
        "Subject cannot be deleted while it is used by a document.");
}
```

The global middleware already maps `InvalidOperationException` to 409.

- [ ] **Step 5: Make the controller derive owner identity**

Remove all `[Authorize(Roles = "Admin")]` attributes from Subject writes. Add one private JWT helper matching the established controller pattern:

```csharp
private Guid GetCurrentUserId()
{
    var claim = User.FindFirst(ClaimTypes.NameIdentifier)
        ?? User.FindFirst("sub")
        ?? User.FindFirst("userId");

    return claim != null && Guid.TryParse(claim.Value, out var userId)
        ? userId
        : Guid.Empty;
}
```

Every endpoint returns `Unauthorized()` for an empty current ID and calls an owner-explicit service method. Foreign resources resolve to `NotFound()`.

- [ ] **Step 6: Verify and commit owner-scoped CRUD**

Run:

```powershell
dotnet build AIStudyHub.slnx --no-restore
```

Then:

```powershell
git add AIStudyHub.Business/Interfaces/Services/ISubjectService.cs AIStudyHub.Business/Services/ModuleServices.cs AIStudyHub.API/Controllers/SubjectController.cs
git diff --cached --check
git commit -m "refactor: scope subject CRUD to students"
```

---

### Task 4: Enforce Subject Ownership in Document Creation

**Files:**
- Modify: `AIStudyHub.Business/Services/ModuleServices.cs`

**Interfaces:**
- Consumes: `Subject.OwnerUserId`
- Preserves: current `IDocumentService.CreateAsync(CreateDocumentRequestDto, ...)`

- [ ] **Step 1: Replace the global Subject existence check**

In `DocumentService.CreateAsync`, replace `GetByIdAsync` with:

```csharp
var subjectExists = await _unitOfWork.Subjects.Query()
    .AnyAsync(
        subject => subject.Id == request.SubjectId
            && subject.OwnerUserId == request.UserId,
        cancellationToken);
```

If false, throw `KeyNotFoundException("Subject not found.")` so missing and foreign Subjects both become 404.

- [ ] **Step 2: Verify and commit**

```powershell
dotnet build AIStudyHub.slnx --no-restore
git add AIStudyHub.Business/Services/ModuleServices.cs
git diff --cached --check
git commit -m "fix: require owned subjects for documents"
```

---

### Task 5: Generate and Inspect the Ownership/Cleanup Migration

**Files:**
- Create: `AIStudyHub.Data/Migrations/*_OwnSubjectsAndCleanDemoLearningData.cs`
- Create: `AIStudyHub.Data/Migrations/*_OwnSubjectsAndCleanDemoLearningData.Designer.cs`
- Modify: `AIStudyHub.Data/Migrations/ApplicationDbContextModelSnapshot.cs`

**Interfaces:**
- Produces final schema: required `Subjects.owner_user_id`
- Produces final unique index: `(owner_user_id, subject_code)`
- Preserves Users, roles, tiers, payments, and authentication records

- [ ] **Step 1: Generate the new migration**

Set the required design-time secrets only for the command, then run:

```powershell
$env:Jwt__SecretKey = '0000000000000000000000000000000000000000000000000000000000000000'
$env:ConnectionStrings__DefaultConnection = 'Server=(localdb)\MSSQLLocalDB;Database=AIStudyHubDesignTime;Trusted_Connection=True;TrustServerCertificate=True'
dotnet ef migrations add OwnSubjectsAndCleanDemoLearningData --project AIStudyHub.Data/AIStudyHub.Data.csproj --startup-project AIStudyHub.API/AIStudyHub.API.csproj
```

Do not apply the migration.

- [ ] **Step 2: Add deterministic cleanup operations to the newly generated migration**

Before deleting seeded Subjects or making `owner_user_id` required, add SQL
cleanup in dependency-safe order. Use the exact table names configured in
`EntityConfigurations.cs`:

```sql
DELETE FROM [Notification];
DELETE FROM [Recommendations];
DELETE FROM [UserBadge];
DELETE FROM [StudyLogs];
DELETE FROM [TokenLedger];
DELETE FROM [ChatSession];
DELETE FROM [Document];
DELETE FROM [Subjects];

UPDATE [UserStats]
SET [total_xp] = 0,
    [current_level] = 1,
    [current_streak] = 0,
    [best_streak] = 0,
    [last_activity_date] = NULL,
    [total_study_seconds] = 0;

UPDATE [Users]
SET [current_storage_capacity] = 0,
    [current_ai_token_usage] = 0;
```

The `ChatSession` delete cascades its messages, citation snapshots, and session
links. The `Document` delete cascades votes, reports, shares, Flashcards/current
reviews, Quizzes/questions/answers/submissions, and remaining session links.
`StudyLogs` is deliberately deleted first instead of relying on its `SetNull`
relationship. Keep Users, roles, refresh/OTP records, tiers, Payments, and Badge
definitions.

Verify the exact table/column names again against the generated model before
inserting this SQL. Keep the generated schema operations for dropping the old
index, adding ownership, adding the foreign key, and creating the composite
index.

- [ ] **Step 3: Make Down schema-safe without fabricating deleted demo data**

`Down` may restore the old schema/index shape but must not attempt to recreate deleted Documents, chats, learning history, or user statistics. It may restore the historical Subject seed rows only if EF generated deterministic `InsertData` operations for the final model transition.

- [ ] **Step 4: Prove existing migrations were not touched**

Run:

```powershell
git status --short AIStudyHub.Data/Migrations
git diff --name-only -- AIStudyHub.Data/Migrations
git diff --check
```

Expected:

- Only the two new migration files and snapshot are changed.
- No older migration appears.

- [ ] **Step 5: Build and commit the migration**

```powershell
dotnet build AIStudyHub.slnx --no-restore
git add AIStudyHub.Data/Migrations
git diff --cached --check
git commit -m "refactor: migrate to student-owned subjects"
```

Do not run `database update`.

This migration cleans SQL data only. Do not add code that silently deletes
physical uploads or Qdrant vectors during startup/migration. After the repository
owner separately authorizes applying the migration and environment cleanup,
report the configured upload root and Qdrant collection as two explicit cleanup
targets for their approval.

---

### Task 6: Update Registration and Subject Documentation

**Files:**
- Modify: `README.md`
- Modify: `AGENT.md`
- Modify: `ARCHITECTURE.md`
- Modify: `docs/FRONTEND_GUIDE.md`

**Interfaces:**
- Documents: register without `dateOfBirth`
- Documents: authenticated student-owned Subject CRUD

- [ ] **Step 1: Update active contracts**

Remove `dateOfBirth` from registration examples only. Keep profile/admin date-of-birth documentation.

Change Subject documentation from Admin/global to:

```text
GET/POST/PUT/DELETE /api/Subject
Authenticated student; results and writes are owner-scoped.
```

Document the 404 behavior for foreign Subjects and 409 behavior for referenced deletion.

- [ ] **Step 2: Remove active hard-coded/global claims**

Run:

```powershell
rg -n -i "hard.?coded subject|Admin.*Subject|global Subject|register.*dateOfBirth" README.md AGENT.md ARCHITECTURE.md docs/FRONTEND_GUIDE.md
```

Update every active occurrence that contradicts the new contract.

- [ ] **Step 3: Build and commit documentation**

```powershell
dotnet build AIStudyHub.slnx --no-restore
git add README.md AGENT.md ARCHITECTURE.md docs/FRONTEND_GUIDE.md
git diff --cached --check
git commit -m "docs: describe student-owned subjects"
```

---

## Manual Verification Handoff

Do not execute these as automated or smoke tests. Give them to the repository owner:

1. Register without `dateOfBirth`; confirm registration and OTP flow work.
2. Confirm profile/admin date-of-birth contracts still exist.
3. Student A creates `PRN`; Student B also creates `PRN`.
4. Student A cannot create a second `PRN`.
5. Student A cannot read/update/delete Student B's Subject.
6. Upload/Document creation rejects Student B's Subject for Student A.
7. Deleting an unused owned Subject returns 204.
8. Deleting a Subject referenced by a Document returns 409.
9. Review the generated migration before separately authorizing database/Qdrant/disk cleanup.
