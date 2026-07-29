# Identity and Subject Ownership Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove date of birth and replace seeded global Subjects with private student-owned CRUD.

**Architecture:** User contracts lose `DateOfBirth` end-to-end. Subject services accept the authenticated owner ID explicitly, enforce owner-scoped queries, and reject deletion when a Document references the Subject.

**Tech Stack:** ASP.NET Core 8, EF Core 8, SQL Server, AutoMapper, FluentValidation, xUnit

## Global Constraints

- Keep exactly the existing API, Business, and Data projects.
- Keep every existing migration file unchanged; add a new migration.
- Ownership comes only from authenticated JWT claims.
- Admin has no cross-user Subject override.
- Foreign-owned resources return `404 Not Found`.
- Referenced Subject deletion returns `409 Conflict`.
- The development/demo database may be recreated after the migration.

---

### Task 1: Remove DateOfBirth from registration and user contracts

**Files:**
- Modify: `AIStudyHub.Data/Entities/User.cs`
- Modify: `AIStudyHub.Data/Configurations/EntityConfigurations.cs`
- Modify: `AIStudyHub.Business/DTOs/Authentication/AuthDtos.cs`
- Modify: `AIStudyHub.Business/DTOs/Users/UserDtos.cs`
- Modify: `AIStudyHub.Business/Validators/Authentication/AuthValidators.cs`
- Modify: `AIStudyHub.Business/Validators/Users/UserValidators.cs`
- Modify: `AIStudyHub.Business/Features/Users/Commands/PatchUserCommand.cs`
- Modify: `AIStudyHub.Business/Services/AuthService.cs`
- Modify: `AIStudyHub.Business/Services/UserService.cs`
- Modify: `AIStudyHub.Business/Mappings/ApplicationMappingProfile.cs`
- Test: `AIStudyHub.Tests/Services/AuthServiceTests.cs`
- Create: `AIStudyHub.Tests/Services/UserContractTests.cs`

**Interfaces:**
- Produces: `RegisterRequestDto(string FullName, string Email, string Password)`
- Produces: `UpdateProfileRequestDto(string FullName)`
- Produces: user create/update/patch/response DTOs without `DateOfBirth`

- [ ] **Step 1: Add failing contract tests**

```csharp
public sealed class UserContractTests
{
    [Theory]
    [InlineData(typeof(RegisterRequestDto))]
    [InlineData(typeof(UserResponseDto))]
    [InlineData(typeof(CreateUserRequestDto))]
    [InlineData(typeof(UpdateUserRequestDto))]
    [InlineData(typeof(PatchUserRequestDto))]
    [InlineData(typeof(UpdateProfileRequestDto))]
    public void UserContracts_DoNotExposeDateOfBirth(Type contractType)
    {
        Assert.DoesNotContain(
            contractType.GetProperties(),
            property => property.Name.Equals("DateOfBirth", StringComparison.OrdinalIgnoreCase));
    }
}
```

- [ ] **Step 2: Run the contract tests and confirm failure**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --no-restore --filter FullyQualifiedName~UserContractTests`

Expected: FAIL because the listed contracts still expose `DateOfBirth`.

- [ ] **Step 3: Remove DateOfBirth end-to-end**

Use these final record shapes and remove all entity, mapping, service assignment, and validation references:

```csharp
public sealed record RegisterRequestDto(
    string FullName,
    string Email,
    string Password);

public sealed record UpdateProfileRequestDto(string FullName);
```

Keep the existing non-date fields in admin user DTOs in their current order. Update `PatchUserCommand` so its reconstructed `UpdateUserRequestDto` contains only remaining properties.

- [ ] **Step 4: Run focused user/auth tests**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --no-restore --filter "FullyQualifiedName~AuthServiceTests|FullyQualifiedName~UserServiceTests|FullyQualifiedName~UserContractTests"`

Expected: PASS.

- [ ] **Step 5: Commit the contract removal**

```powershell
git add AIStudyHub.Data/Entities/User.cs AIStudyHub.Data/Configurations/EntityConfigurations.cs AIStudyHub.Business/DTOs/Authentication/AuthDtos.cs AIStudyHub.Business/DTOs/Users/UserDtos.cs AIStudyHub.Business/Validators/Authentication/AuthValidators.cs AIStudyHub.Business/Validators/Users/UserValidators.cs AIStudyHub.Business/Features/Users/Commands/PatchUserCommand.cs AIStudyHub.Business/Services/AuthService.cs AIStudyHub.Business/Services/UserService.cs AIStudyHub.Business/Mappings/ApplicationMappingProfile.cs AIStudyHub.Tests/Services/AuthServiceTests.cs AIStudyHub.Tests/Services/UserContractTests.cs
git commit -m "refactor: remove user date of birth"
```

### Task 2: Add Subject ownership to the data model

**Files:**
- Modify: `AIStudyHub.Data/Entities/Subject.cs`
- Modify: `AIStudyHub.Data/Entities/User.cs`
- Modify: `AIStudyHub.Data/Configurations/EntityConfigurations.cs`
- Create: `AIStudyHub.Tests/Services/SubjectOwnershipModelTests.cs`

**Interfaces:**
- Produces: `Subject.OwnerUserId : Guid`
- Produces: `Subject.OwnerUser : User`
- Produces: `User.Subjects : ICollection<Subject>`
- Produces: unique index `(OwnerUserId, SubjectCode)`

- [ ] **Step 1: Write a failing EF model test**

```csharp
[Fact]
public void SubjectModel_UsesOwnerScopedCodeAndNoSeedData()
{
    var options = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlite("DataSource=:memory:")
        .Options;
    using var context = new ApplicationDbContext(options);
    var entity = context.Model.FindEntityType(typeof(Subject))!;
    Assert.NotNull(entity.FindProperty(nameof(Subject.OwnerUserId)));
    Assert.Contains(entity.GetIndexes(), index =>
        index.IsUnique &&
        index.Properties.Select(property => property.Name)
            .SequenceEqual(new[] { nameof(Subject.OwnerUserId), nameof(Subject.SubjectCode) }));
    Assert.Empty(entity.GetSeedData());
}
```

- [ ] **Step 2: Run the model test and confirm failure**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --no-restore --filter FullyQualifiedName~SubjectOwnershipModelTests`

Expected: FAIL because ownership does not exist and seed data is present.

- [ ] **Step 3: Implement the owned Subject model**

```csharp
public sealed class Subject : BaseEntity
{
    public Guid OwnerUserId { get; set; }
    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public User OwnerUser { get; set; } = null!;
    public ICollection<Document> Documents { get; set; } = [];
}
```

Configure the relationship with `DeleteBehavior.Restrict`, remove the complete `HasData` block, remove the global unique code index, and add:

```csharp
builder.Property(x => x.OwnerUserId).HasColumnName("owner_user_id").IsRequired();
builder.HasIndex(x => new { x.OwnerUserId, x.SubjectCode }).IsUnique();
builder.HasOne(x => x.OwnerUser)
    .WithMany(x => x.Subjects)
    .HasForeignKey(x => x.OwnerUserId)
    .OnDelete(DeleteBehavior.Restrict);
```

- [ ] **Step 4: Run the model test**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --no-restore --filter FullyQualifiedName~SubjectOwnershipModelTests`

Expected: PASS.

- [ ] **Step 5: Commit the owned model**

```powershell
git add AIStudyHub.Data/Entities/Subject.cs AIStudyHub.Data/Entities/User.cs AIStudyHub.Data/Configurations/EntityConfigurations.cs AIStudyHub.Tests/Services/SubjectOwnershipModelTests.cs
git commit -m "feat: add student ownership to subjects"
```

### Task 3: Enforce owner-scoped Subject CRUD

**Files:**
- Modify: `AIStudyHub.Business/Interfaces/Services/ISubjectService.cs`
- Modify: `AIStudyHub.Business/Services/ModuleServices.cs`
- Modify: `AIStudyHub.API/Controllers/SubjectController.cs`
- Create: `AIStudyHub.Tests/Services/SubjectServiceTests.cs`

**Interfaces:**
- Produces: `GetAllPagedAsync(Guid ownerUserId, PaginationParams params, CancellationToken ct)`
- Produces: `GetByIdAsync(Guid id, Guid ownerUserId, CancellationToken ct)`
- Produces: `CreateAsync(Guid ownerUserId, CreateSubjectRequestDto request, CancellationToken ct)`
- Produces: `UpdateAsync(Guid id, Guid ownerUserId, UpdateSubjectRequestDto request, CancellationToken ct)`
- Produces: `DeleteAsync(Guid id, Guid ownerUserId, CancellationToken ct)`

- [ ] **Step 1: Write failing service tests for isolation and conflicts**

```csharp
[Fact]
public async Task GetAllPagedAsync_ReturnsOnlyOwnersSubjects()
{
    var result = await _service.GetAllPagedAsync(_ownerId, new PaginationParams(), default);
    Assert.All(result.Items, subject => Assert.Contains(subject.Id, _ownerSubjectIds));
}

[Fact]
public async Task DeleteAsync_WhenDocumentReferencesSubject_ThrowsConflict()
{
    var exception = await Assert.ThrowsAsync<InvalidOperationException>(
        () => _service.DeleteAsync(_referencedSubjectId, _ownerId));
    Assert.Equal("Subject is used by one or more documents.", exception.Message);
}

[Fact]
public async Task UpdateAsync_ForForeignSubject_ThrowsNotFound()
{
    await Assert.ThrowsAsync<KeyNotFoundException>(() =>
        _service.UpdateAsync(_foreignSubjectId, _ownerId,
            new UpdateSubjectRequestDto("OWN", "Owned", null)));
}
```

- [ ] **Step 2: Run the service tests and confirm failure**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --no-restore --filter FullyQualifiedName~SubjectServiceTests`

Expected: FAIL because current service signatures and ownership rules differ.

- [ ] **Step 3: Replace the generic CRUD inheritance with explicit owned methods**

Implement all queries with both ID and owner ID. Normalize `SubjectCode` with `Trim().ToUpperInvariant()` before uniqueness checks. Before delete, check:

```csharp
var isReferenced = await _unitOfWork.Documents.Query()
    .AnyAsync(document => document.SubjectId == id, cancellationToken);
if (isReferenced)
    throw new InvalidOperationException("Subject is used by one or more documents.");
```

Update the controller to derive `userId` from claims for every action, remove Admin role attributes, return 404 for `KeyNotFoundException`, and map the exact referenced-subject exception to 409.

- [ ] **Step 4: Run Subject tests**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --no-restore --filter "FullyQualifiedName~SubjectServiceTests|FullyQualifiedName~SubjectOwnershipModelTests"`

Expected: PASS.

- [ ] **Step 5: Commit Subject CRUD**

```powershell
git add AIStudyHub.Business/Interfaces/Services/ISubjectService.cs AIStudyHub.Business/Services/ModuleServices.cs AIStudyHub.API/Controllers/SubjectController.cs AIStudyHub.Tests/Services/SubjectServiceTests.cs
git commit -m "feat: scope subject crud to current student"
```

### Task 4: Require owned Subjects during upload

**Files:**
- Modify: `AIStudyHub.API/Controllers/DocumentController.cs`
- Create: `AIStudyHub.Tests/Controllers/DocumentSubjectOwnershipContractTests.cs`

**Interfaces:**
- Consumes: `Subject.OwnerUserId`
- Produces: empty Subject ID → 400; absent or foreign Subject → 404

- [ ] **Step 1: Write controller contract tests**

```csharp
[Fact]
public async Task Upload_WithEmptySubjectId_ReturnsBadRequest()
{
    var result = await _controller.UploadDocumentFile(
        Request(subjectId: Guid.Empty), default);
    Assert.IsType<BadRequestObjectResult>(result.Result);
}

[Fact]
public async Task Upload_WithForeignSubject_ReturnsNotFound()
{
    var result = await _controller.UploadDocumentFile(
        Request(subjectId: _foreignSubjectId), default);
    Assert.IsType<NotFoundObjectResult>(result.Result);
}
```

- [ ] **Step 2: Run and confirm failure**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --no-restore --filter FullyQualifiedName~DocumentSubjectOwnershipContractTests`

Expected: FAIL because foreign Subjects are currently accepted.

- [ ] **Step 3: Implement the owner query**

```csharp
if (request.SubjectId == Guid.Empty)
    return BadRequest("SubjectId is required.");

var subject = await _unitOfWork.Subjects.Query()
    .AsNoTracking()
    .FirstOrDefaultAsync(
        value => value.Id == request.SubjectId && value.OwnerUserId == userId,
        cancellationToken);
if (subject is null)
    return NotFound("Subject not found.");
```

- [ ] **Step 4: Run the controller tests**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --no-restore --filter FullyQualifiedName~DocumentSubjectOwnershipContractTests`

Expected: PASS.

- [ ] **Step 5: Commit upload ownership**

```powershell
git add AIStudyHub.API/Controllers/DocumentController.cs AIStudyHub.Tests/Controllers/DocumentSubjectOwnershipContractTests.cs
git commit -m "fix: require owned subject for document upload"
```

### Task 5: Generate and verify the schema migration

**Files:**
- Create: `AIStudyHub.Data/Migrations/20260729090000_RemoveDateOfBirthAndOwnSubjects.cs`
- Create: `AIStudyHub.Data/Migrations/20260729090000_RemoveDateOfBirthAndOwnSubjects.Designer.cs`
- Modify: `AIStudyHub.Data/Migrations/ApplicationDbContextModelSnapshot.cs`

**Interfaces:**
- Produces: final schema without `Users.dob`
- Produces: `Subjects.owner_user_id` and owner-scoped unique index
- Produces: no seeded Subject rows in the final model

- [ ] **Step 1: Generate the migration**

Run:

```powershell
dotnet ef migrations add RemoveDateOfBirthAndOwnSubjects --project AIStudyHub.Data/AIStudyHub.Data.csproj --startup-project AIStudyHub.API/AIStudyHub.API.csproj
```

Expected: one new migration pair and an updated model snapshot; no old migration changes.

- [ ] **Step 2: Inspect the migration**

Confirm `Up` drops `dob`, deletes the historical seeded rows, adds `owner_user_id`, recreates the code index as `(owner_user_id, subject_code)`, and adds the owner FK. Because the demo DB is recreated, a deterministic placeholder default may be used only for the migration operation and removed before the final required constraint.

- [ ] **Step 3: Verify migration history files were not modified**

Run: `git status --short AIStudyHub.Data/Migrations`

Expected: only the new migration pair and snapshot are changed.

- [ ] **Step 4: Build and run all tests**

Run: `dotnet test AIStudyHub.slnx --no-restore`

Expected: PASS.

- [ ] **Step 5: Commit the migration**

```powershell
git add AIStudyHub.Data/Migrations
git commit -m "db: remove date of birth and own subjects"
```
