# Hosted Worker Registration Deduplication Design

## Objective

Ensure every background worker is registered exactly once and each streak worker has one clear responsibility. This change is limited to registration ownership and duplicate streak-warning behavior; scheduling, UTC semantics, thresholds, and notification copy remain unchanged.

## Current Problems

`StreakWarningWorker` and `QuotaWarningWorker` are registered in both `Program.cs` and `BusinessServiceExtensions.AddBusinessServices`. The host can therefore start two instances of each worker and produce duplicate work or notifications.

`DailyStreakResetWorker` also contains streak-warning logic even though `StreakWarningWorker` owns the same behavior. The duplication exists at both the dependency-injection and business-behavior levels.

Worker registrations are currently split between the API composition root and the Business service-registration extension, making the complete worker set difficult to review.

## Scope

### In scope

- Make `BusinessServiceExtensions.AddBusinessServices` the single registration point for all Business-layer hosted workers.
- Remove hosted-worker registrations from `Program.cs`.
- Register every existing worker exactly once.
- Remove warning state, dependencies, and behavior from `DailyStreakResetWorker`.
- Keep `StreakWarningWorker` as the only streak-warning implementation.
- Improve cancellation handling where necessary so normal host shutdown does not appear as a worker failure.
- Add regression tests for worker registration and the retained streak-warning behavior.

### Out of scope

- Changing UTC to a user or application timezone.
- Changing warning/reset hours, scan intervals, quota thresholds, or notification text.
- Introducing cron, Hangfire, a scheduler abstraction, or a common worker base class.
- Refactoring token quota calculation or OTP policy.

## Architecture

`Program.cs` remains the application composition root and invokes `AddBusinessServices(configuration)`, but it does not enumerate Business-layer workers itself. `BusinessServiceExtensions` owns both scoped Business services and hosted Business processes.

The resulting registration boundary is:

```text
Program.cs
    -> AddBusinessServices(configuration)
        -> DocumentBackgroundProcessor
        -> TierExpiryWorker
        -> UnverifiedAccountCleanupService
        -> TierExpirationCleanupService
        -> DailyStreakResetWorker
        -> StreakWarningWorker
        -> QuotaWarningWorker
```

No worker type may be registered by both layers.

## Responsibilities and Data Flow

### DailyStreakResetWorker

The reset worker wakes on its existing interval, checks the existing UTC reset condition, loads stale active streaks, resets them, and persists the changes. It no longer resolves `IRealTimeNotificationService`, tracks a warning date, or sends warning notifications.

### StreakWarningWorker

The warning worker remains the sole owner of streak-at-risk warnings. On its existing schedule it finds eligible users, creates notification-history rows, attempts realtime delivery for each user, and persists the notification rows.

### QuotaWarningWorker

The quota worker retains its current quota calculation, idempotency check, schedule, threshold, and notification content. Only its duplicate registration is removed.

## Error and Cancellation Handling

- A failure during a worker iteration is logged without terminating the application host.
- Failure to deliver realtime notification for one user does not prevent processing other users.
- Persisted notification history is authoritative; realtime delivery remains best-effort.
- `OperationCanceledException` caused by the host cancellation token ends the worker normally and is not logged as an application error.
- This change does not introduce retry behavior or alter existing idempotency rules.

## Testing Strategy

Add a service-registration regression test that invokes `AddBusinessServices` with test configuration and asserts that every expected hosted worker implementation is registered exactly once.

Retain or add focused behavior tests that verify `StreakWarningWorker` creates one notification and attempts one realtime delivery for an eligible user. Verify cancellation allows worker execution to stop normally without an unexpected failure.

Compilation provides a structural check that `DailyStreakResetWorker` no longer depends on notification behavior. Tests should construct it using only its reset dependencies where practical.

Before completion, run the focused worker/service-registration tests, the full test suite, and a solution build.

## Acceptance Criteria

- Each existing hosted worker is registered exactly once.
- `Program.cs` contains no Business hosted-worker registrations.
- `DailyStreakResetWorker` performs reset behavior only.
- `StreakWarningWorker` is the only component that sends streak-at-risk warnings.
- Existing UTC schedules, thresholds, and user-facing notification content remain unchanged.
- Normal cancellation does not surface as a worker failure.
- The solution builds and all tests pass.

## Follow-up Sequence

After this isolated change is implemented and verified, hardcode cleanup continues in the agreed order: OTP policy unification, token reservation and estimates, timezone/scheduling configuration, AI and gamification options, infrastructure paths, and magic-string/security-log cleanup. Each group receives its own design and implementation cycle.
