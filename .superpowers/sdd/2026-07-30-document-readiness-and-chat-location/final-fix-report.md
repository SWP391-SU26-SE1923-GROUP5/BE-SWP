# Final Fix Wave Report

Date: 2026-07-31
Branch: `refactor`
Starting HEAD: `95a97fda52ec3b263663dea0e1475ba5675a6460`

## Status

Implemented all four final-review findings as one coordinated fix wave. The
final commit SHA is recorded in the parent handoff because this report is part
of that commit.

No unit, integration, E2E, or smoke tests were run, per the task constraint.

## Exact fixes

1. Persisted readiness now owns document notification content.
   - Both document notification interface methods accept a
     `DocumentReadinessDto`.
   - `DocumentBackgroundProcessor` evaluates the persisted `Document` only
     after the success/failure state has been saved and passes that result to
     the notifier.
   - Both SignalR payload types copy `status`, `isChatReady`, `message`, and
     `canRetry` from that evaluator result.
   - Notification title/body selection also uses the evaluator result.
     Supported `Done` keeps the ready copy; retryable `Failed` keeps the safe
     retry copy; unsupported `Done` uses the unsupported message and never
     claims the document is ready.
   - No Qdrant query or raw processing error was added to the notification
     path.

2. The `DOCUMENTS_NOT_READY` 409 response now uses
   `HttpResponse.WriteAsJsonAsync`.
   - This applies ASP.NET web JSON naming to nested
     `BlockingDocumentResponseDto` values, keeping fields such as
     `documentId`, `isChatReady`, and `canRetry` camelCase.

3. `DocumentReadinessEvaluator` now returns the approved unavailable state for:
   - every non-`Active` lifecycle, including `Trashed` and `Purged`;
   - active Documents whose status is `Draft`, `Archived`, `Banned`, or
     status-level `Trashed`.
   - The result remains safe: `isChatReady=false`, `canRetry=false`, and
     `Tài liệu không khả dụng cho Chat.`

4. `DocumentProcessingService` now passes `fileExtension` explicitly as the
   extension argument: `SupportsChat(null, fileExtension)`.
   - The shared policy therefore normalizes both dotted and undotted values,
     including `.pdf` and `pdf`.

5. Active contract documentation was corrected.
   - The design now states that completion/failure SignalR notifications mirror
     persisted evaluator state and that unsupported `Done` must not claim
     readiness.
   - The plan examples now include the readiness parameter, unavailable status
     coverage, evaluator-derived notification behavior, and web JSON
     serialization for the 409 response.

## Files changed

- `AIStudyHub.API/Middleware/GlobalExceptionMiddleware.cs`
- `AIStudyHub.Business/Interfaces/Services/IRealTimeNotificationService.cs`
- `AIStudyHub.Business/Services/DocumentProcessingService.cs`
- `AIStudyHub.Business/Services/DocumentReadinessEvaluator.cs`
- `AIStudyHub.Business/Services/RealTimeNotificationService.cs`
- `AIStudyHub.Business/Workers/DocumentBackgroundProcessor.cs`
- `docs/superpowers/plans/2026-07-30-document-readiness-and-chat-location.md`
- `docs/superpowers/specs/2026-07-30-document-readiness-and-chat-location-design.md`
- `.superpowers/sdd/2026-07-30-document-readiness-and-chat-location/final-fix-report.md`

No migration, model snapshot, project, package, queue, or citation file changed.

## Verification and source audits

### Required build

Command:

```powershell
dotnet build AIStudyHub.slnx --no-restore
```

Final post-commit result: exit code `0`; build succeeded with `0` errors and `1`
warning.

The warning is the pre-existing `CS1587` XML-comment placement warning in
`AIStudyHub.Business/DTOs/Documents/DocumentDtos.cs:49`, which this wave did not
change.

### Interface and call-site audit

```powershell
rg -n -S "NotifyDocumentProcessedAsync|NotifyDocumentFailedAsync" AIStudyHub.API AIStudyHub.Business -g '*.cs'
```

Inspected all six results: two interface declarations, two implementation
methods, and the only two call sites in `DocumentBackgroundProcessor`. Both
worker calls pass readiness evaluated from the saved `Document`.

### Notification payload and privacy audit

```powershell
rg -n -C 8 -S "new DocumentProcessedPayload|new DocumentFailedPayload|BuildDocumentNotificationContent|DocumentReadinessEvaluator\.Evaluate\(document\)" AIStudyHub.Business/Services/RealTimeNotificationService.cs AIStudyHub.Business/Workers/DocumentBackgroundProcessor.cs
rg -n -S "DocumentFailedPayload\(|NotifyDocumentFailedAsync\(|ErrorMessage|ex\.Message" AIStudyHub.API AIStudyHub.Business -g '*.cs'
```

Inspected every result. Document payload construction uses only the friendly
readiness DTO. The worker retains exception text in backend logging and internal
error fields, but it does not pass exception text to REST or SignalR.
Pre-existing unrelated controller/error-marker uses were not changed.

### 409 serialization audit

```powershell
rg -n -C 8 -S "DocumentsNotReadyException|WriteAsJsonAsync\(payload\)" AIStudyHub.API/Middleware/GlobalExceptionMiddleware.cs
```

Confirmed the dedicated catch remains before `InvalidOperationException`, keeps
status `409` and code `DOCUMENTS_NOT_READY`, and serializes the complete payload
through ASP.NET web JSON.

### Readiness and extension-policy audits

```powershell
rg -n -C 8 -S "DocumentLifecycleStatus != DocumentLifecycleStatus.Active|DocumentStatus\.Draft|DocumentStatus\.Trashed|Tài liệu không khả dụng cho Chat" AIStudyHub.Business/Services/DocumentReadinessEvaluator.cs
rg -n -C 4 -S "SupportsChat\(null, fileExtension\)|SupportsChat\(" AIStudyHub.Business/Services/DocumentProcessingService.cs AIStudyHub.Business/AI/DocumentRagFilePolicy.cs AIStudyHub.Business/Services/DocumentReindexPolicy.cs
```

Confirmed lifecycle/status unavailable coverage and explicit extension argument
binding. Existing filename-based callers remain filename-based.

### Scope and diff audits

```powershell
git diff --name-only -- AIStudyHub.Data AIStudyHub.API/*.csproj AIStudyHub.Business/*.csproj AIStudyHub.slnx
git diff --check
```

The forbidden-scope diff was empty. `git diff --check` exited `0`; it emitted
only repository line-ending conversion notices.

## Self-review and concerns

- Re-read the complete diff and verified all notification interface call sites.
- The notifier has no persistence/vector dependency beyond its existing
  notification storage; readiness evaluation stays in the worker after document
  persistence.
- Unsupported `Done` produces `status=Done`, `isChatReady=false`,
  `canRetry=false`, the unsupported message, and non-ready title/body copy.
- Failure notification data follows the evaluator, so lifecycle or unsupported
  precedence cannot be overwritten by hardcoded retry state.
- The middleware change is intentionally limited to the structured readiness
  conflict; unrelated legacy error responses retain their current serialization.
- No unresolved functional concern was found. The pre-existing `CS1587` warning
  noted above is outside this wave.
