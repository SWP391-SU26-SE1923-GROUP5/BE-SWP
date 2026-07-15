# Hosted Worker Registration Deduplication Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Register every Business hosted worker exactly once and make `StreakWarningWorker` the sole owner of streak-at-risk notifications.

**Architecture:** Add one testable Business-layer registration method that owns the complete hosted-worker list, and call it from `AddBusinessServices`. Remove API-level worker registrations. Expose the reset operation as a focused `RunOnceAsync` method and delete all warning behavior from `DailyStreakResetWorker`.

**Tech Stack:** C# 12, ASP.NET Core hosted services, .NET 10 test runner, xUnit, EF Core SQLite, Moq.

## Global Constraints

- Keep the existing UTC scheduling semantics unchanged.
- Keep warning/reset hours, scan intervals, quota thresholds, and notification copy unchanged.
- Do not introduce Hangfire, cron, a scheduler abstraction, or a common worker base class.
- Do not modify OTP or token quota behavior in this implementation.
- Persisted notifications remain authoritative and realtime delivery remains best-effort.
- Do not add Codex as a commit author or co-author.

---

## File Map

- Create `AIStudyHub.Tests/Services/BusinessHostedServiceRegistrationTests.cs`: regression coverage for the single hosted-worker registry.
- Create `AIStudyHub.Tests/Services/StreakWorkerTests.cs`: reset/warning responsibility and cancellation coverage.
- Modify `AIStudyHub.Business/Services/BusinessServiceExtensions.cs`: become the only Business hosted-worker registration owner.
- Modify `AIStudyHub.API/Program.cs`: remove direct hosted-worker registrations.
- Modify `AIStudyHub.Business/Workers/DailyStreakResetWorker.cs`: retain reset behavior only and expose a testable single-run method.
- Modify `AIStudyHub.Business/Workers/StreakWarningWorker.cs`: handle normal cancellation explicitly.
- Modify `AIStudyHub.Business/Workers/QuotaWarningWorker.cs`: handle normal cancellation explicitly.

### Task 1: Centralize Hosted-Worker Registration

**Files:**
- Create: `AIStudyHub.Tests/Services/BusinessHostedServiceRegistrationTests.cs`
- Modify: `AIStudyHub.Business/Services/BusinessServiceExtensions.cs`
- Modify: `AIStudyHub.API/Program.cs`

**Interfaces:**
- Produces: `public static IServiceCollection AddBusinessHostedServices(this IServiceCollection services)`.
- Consumes: existing worker types deriving from `BackgroundService`.

- [ ] **Step 1: Write the failing registration test**

Create the following test. It intentionally references the new registration boundary before it exists:

```csharp
using AIStudyHub.Business.Services;
using AIStudyHub.Business.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AIStudyHub.Tests.Services;

public sealed class BusinessHostedServiceRegistrationTests
{
    [Fact]
    public void AddBusinessHostedServices_RegistersEveryWorkerExactlyOnce()
    {
        var services = new ServiceCollection();

        services.AddBusinessHostedServices();

        var registrations = services
            .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
            .Select(descriptor => descriptor.ImplementationType)
            .ToList();

        Type[] expected =
        [
            typeof(DocumentBackgroundProcessor),
            typeof(TierExpiryWorker),
            typeof(UnverifiedAccountCleanupService),
            typeof(TierExpirationCleanupService),
            typeof(DailyStreakResetWorker),
            typeof(StreakWarningWorker),
            typeof(QuotaWarningWorker)
        ];

        Assert.Equal(expected.Length, registrations.Count);
        foreach (var workerType in expected)
        {
            Assert.Equal(1, registrations.Count(type => type == workerType));
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```powershell
dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --filter "FullyQualifiedName~BusinessHostedServiceRegistrationTests" --no-restore
```

Expected: compilation fails because `AddBusinessHostedServices` does not exist.

- [ ] **Step 3: Add the single registration boundary**

In `BusinessServiceExtensions`, replace the scattered hosted-service block with one call at the end of the service setup:

```csharp
services.AddBusinessHostedServices();
return services;
```

Add this method to the same extension class:

```csharp
public static IServiceCollection AddBusinessHostedServices(this IServiceCollection services)
{
    services.AddHostedService<DocumentBackgroundProcessor>();
    services.AddHostedService<TierExpiryWorker>();
    services.AddHostedService<UnverifiedAccountCleanupService>();
    services.AddHostedService<TierExpirationCleanupService>();
    services.AddHostedService<DailyStreakResetWorker>();
    services.AddHostedService<StreakWarningWorker>();
    services.AddHostedService<QuotaWarningWorker>();
    return services;
}
```

Remove the existing individual `AddHostedService` calls earlier in `AddBusinessServices` so the new method is the only registration path.

In `Program.cs`, delete these registrations:

```csharp
builder.Services.AddHostedService<UnverifiedAccountCleanupService>();
builder.Services.AddHostedService<TierExpirationCleanupService>();
builder.Services.AddHostedService<DailyStreakResetWorker>();
builder.Services.AddHostedService<StreakWarningWorker>();
builder.Services.AddHostedService<QuotaWarningWorker>();
```

- [ ] **Step 4: Run the focused test and inspect registrations**

Run:

```powershell
dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --filter "FullyQualifiedName~BusinessHostedServiceRegistrationTests" --no-restore
rg -n "AddHostedService" AIStudyHub.API/Program.cs AIStudyHub.Business/Services/BusinessServiceExtensions.cs
```

Expected: the test passes; `Program.cs` has no match; all seven registrations appear only inside `AddBusinessHostedServices`.

- [ ] **Step 5: Commit the registration change**

```powershell
git add AIStudyHub.Tests/Services/BusinessHostedServiceRegistrationTests.cs AIStudyHub.Business/Services/BusinessServiceExtensions.cs AIStudyHub.API/Program.cs
git commit -m "fix: register hosted workers once"
```

### Task 2: Separate Streak Reset from Warning Behavior

**Files:**
- Create: `AIStudyHub.Tests/Services/StreakWorkerTests.cs`
- Modify: `AIStudyHub.Business/Workers/DailyStreakResetWorker.cs`

**Interfaces:**
- Produces: `public Task RunOnceAsync(CancellationToken cancellationToken)` on `DailyStreakResetWorker`.
- Consumes: `ApplicationDbContext` from a scope created through `IServiceProvider`.

- [ ] **Step 1: Write the failing reset-responsibility test fixture**

Create a SQLite-backed fixture following the repository's worker-test pattern:

```csharp
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Workers;
using AIStudyHub.Data;
using AIStudyHub.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace AIStudyHub.Tests.Services;

public sealed class StreakWorkerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _dbContext;
    private readonly ServiceProvider _provider;
    private readonly Mock<IRealTimeNotificationService> _notifier = new();

    public StreakWorkerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        _dbContext = new ApplicationDbContext(options);
        _dbContext.Database.EnsureCreated();

        var services = new ServiceCollection();
        services.AddSingleton(_dbContext);
        services.AddSingleton(_notifier.Object);
        _provider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task DailyReset_RunOnceAsync_ResetsStaleStreakWithoutWarning()
    {
        var userId = await AddUserWithStreakAsync(currentStreak: 5);
        var worker = new DailyStreakResetWorker(
            _provider,
            Mock.Of<ILogger<DailyStreakResetWorker>>());

        await worker.RunOnceAsync(CancellationToken.None);

        var stats = await _dbContext.UserStats.SingleAsync(x => x.UserId == userId);
        Assert.Equal(0, stats.CurrentStreak);
        _notifier.Verify(x => x.NotifyStreakAtRiskAsync(
            It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private async Task<Guid> AddUserWithStreakAsync(int currentStreak)
    {
        var userId = Guid.NewGuid();
        _dbContext.Users.Add(new User
        {
            Id = userId,
            FullName = "Streak User",
            Email = $"{userId}@test.local",
            PasswordHash = "hash",
            TierId = Guid.Parse("11111111-1111-1111-1111-111111111111")
        });
        _dbContext.UserStats.Add(new UserStats
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CurrentStreak = currentStreak,
            BestStreak = currentStreak,
            LastActivityDate = DateTime.UtcNow.Date.AddDays(-1)
        });
        await _dbContext.SaveChangesAsync();
        return userId;
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _provider.Dispose();
        _connection.Dispose();
    }
}
```

- [ ] **Step 2: Run the focused test to verify it fails**

Run:

```powershell
dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --filter "FullyQualifiedName~StreakWorkerTests.DailyReset" --no-restore
```

Expected: compilation fails because `DailyStreakResetWorker.RunOnceAsync` does not exist.

- [ ] **Step 3: Remove warning behavior and expose the reset operation**

In `DailyStreakResetWorker.cs`:

- Remove the `AIStudyHub.Business.Interfaces.Services` and notification-related imports.
- Remove `WarnHourUtc`, `HoursUntilReset`, `_lastWarnDate`, and `WarnStaleStreaksAsync`.
- Update the class summary so it describes reset behavior only.
- Remove the noon-warning branch from `ExecuteAsync`.
- Rename `ResetStaleStreaksAsync` to the public single-run operation:

```csharp
public async Task RunOnceAsync(CancellationToken cancellationToken)
{
    using var scope = _serviceProvider.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    var today = DateTime.UtcNow.Date;
    var stale = await db.UserStats
        .Where(s => s.CurrentStreak > 0
                    && (s.LastActivityDate == null || s.LastActivityDate.Value.Date < today))
        .ToListAsync(cancellationToken);

    if (stale.Count == 0)
    {
        _logger.LogInformation("DailyStreakResetWorker: no stale streaks found.");
        return;
    }

    foreach (var stats in stale)
    {
        stats.CurrentStreak = 0;
        stats.UpdatedAt = DateTime.UtcNow;
    }

    await db.SaveChangesAsync(cancellationToken);
    _logger.LogInformation(
        "DailyStreakResetWorker: reset streak for {Count} users.", stale.Count);
}
```

Change the scheduled call to:

```csharp
await RunOnceAsync(stoppingToken);
```

- [ ] **Step 4: Run the focused streak test**

```powershell
dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --filter "FullyQualifiedName~StreakWorkerTests.DailyReset" --no-restore
```

Expected: one test passes; the stale streak becomes zero and the notifier is never called.

- [ ] **Step 5: Commit the responsibility split**

```powershell
git add AIStudyHub.Tests/Services/StreakWorkerTests.cs AIStudyHub.Business/Workers/DailyStreakResetWorker.cs
git commit -m "refactor: isolate daily streak reset"
```

### Task 3: Preserve Warning Behavior and Normalize Cancellation

**Files:**
- Modify: `AIStudyHub.Tests/Services/StreakWorkerTests.cs`
- Modify: `AIStudyHub.Business/Workers/StreakWarningWorker.cs`
- Modify: `AIStudyHub.Business/Workers/QuotaWarningWorker.cs`

**Interfaces:**
- Consumes: existing `StreakWarningWorker.RunOnceAsync(CancellationToken)`.
- Preserves: existing warning title, message, `NotificationType.System`, and `NotifyStreakAtRiskAsync` arguments.

- [ ] **Step 1: Add characterization coverage for the retained warning owner**

Add this test to `StreakWorkerTests`:

```csharp
[Fact]
public async Task StreakWarning_RunOnceAsync_PersistsAndBroadcastsOnce()
{
    var userId = await AddUserWithStreakAsync(currentStreak: 5);
    var worker = new StreakWarningWorker(
        _provider.GetRequiredService<IServiceScopeFactory>(),
        Mock.Of<ILogger<StreakWarningWorker>>());

    await worker.RunOnceAsync(CancellationToken.None);

    var notification = await _dbContext.Notifications.SingleAsync(x => x.UserId == userId);
    Assert.Equal("Streak at risk!", notification.Title);
    Assert.Equal("Your 5-day streak ends in 11h. Review a flashcard now.", notification.Message);
    _notifier.Verify(x => x.NotifyStreakAtRiskAsync(
        userId, 5, 11, It.IsAny<CancellationToken>()), Times.Once);
}
```

- [ ] **Step 2: Run the characterization test before changing worker loops**

```powershell
dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --filter "FullyQualifiedName~StreakWorkerTests.StreakWarning" --no-restore
```

Expected: the test passes, proving warning ownership and copy are preserved before cancellation edits.

- [ ] **Step 3: Make cancellation an explicit normal exit**

Replace the loop in `StreakWarningWorker.ExecuteAsync` with:

```csharp
while (!stoppingToken.IsCancellationRequested)
{
    try
    {
        var now = DateTime.UtcNow;
        if (now.Date != _lastRunDate && now.Hour >= 12)
        {
            await RunOnceAsync(stoppingToken);
            _lastRunDate = now.Date;
        }
    }
    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
    {
        break;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "StreakWarningWorker iteration failed");
    }

    try
    {
        await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
    }
    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
    {
        break;
    }
}
```

Replace the loop in `QuotaWarningWorker.ExecuteAsync` with:

```csharp
while (!stoppingToken.IsCancellationRequested)
{
    try
    {
        var now = DateTime.UtcNow;
        if (now.Date != _lastRunDate && now.Hour >= 9)
        {
            await RunOnceAsync(stoppingToken);
            _lastRunDate = now.Date;
        }
    }
    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
    {
        break;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "QuotaWarningWorker iteration failed");
    }

    try
    {
        await Task.Delay(ScanInterval, stoppingToken);
    }
    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
    {
        break;
    }
}
```

Do not change the scheduling conditions, delays, or exception handling inside either `RunOnceAsync` method.

- [ ] **Step 4: Run focused and full verification**

Run:

```powershell
dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --filter "FullyQualifiedName~BusinessHostedServiceRegistrationTests|FullyQualifiedName~StreakWorkerTests" --no-restore
dotnet test AIStudyHub.slnx --no-restore
dotnet build AIStudyHub.slnx --no-restore
git diff --check
```

Expected: all focused tests pass, the full suite passes, the solution builds with zero errors, and `git diff --check` prints no errors.

- [ ] **Step 5: Confirm the architectural invariants**

Run:

```powershell
rg -n "AddHostedService" AIStudyHub.API/Program.cs AIStudyHub.Business/Services/BusinessServiceExtensions.cs
rg -n "WarnStaleStreaksAsync|NotifyStreakAtRiskAsync|_lastWarnDate" AIStudyHub.Business/Workers/DailyStreakResetWorker.cs AIStudyHub.Business/Workers/StreakWarningWorker.cs
```

Expected: hosted-service registrations exist only in `BusinessServiceExtensions`; warning symbols exist only in `StreakWarningWorker`.

- [ ] **Step 6: Commit cancellation and verification coverage**

```powershell
git add AIStudyHub.Tests/Services/StreakWorkerTests.cs AIStudyHub.Business/Workers/StreakWarningWorker.cs AIStudyHub.Business/Workers/QuotaWarningWorker.cs
git commit -m "fix: stop notification workers cleanly"
```

## Completion Gate

Before declaring the implementation complete, invoke `superpowers:verification-before-completion`, re-run the commands from Task 3 Step 4 against the final worktree, inspect `git status --short`, and report exact test/build results. Do not begin OTP cleanup until this worker change is complete and reviewed.
