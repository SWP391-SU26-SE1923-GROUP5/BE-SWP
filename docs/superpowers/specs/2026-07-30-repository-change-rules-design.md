# Repository Change Rules Design

**Date:** 2026-07-30

## Goal

Update `AGENT.md` and `ARCHITECTURE.md` so every future agent follows the
repository owner's migration and testing policy without relying on implicit
context.

## Non-negotiable migration policy

- A new EF Core migration may be created when an approved schema change
  requires one.
- Every migration that already exists in the repository is immutable.
- Existing migration `.cs` files, designer files, names, timestamps, ordering,
  and historical model operations must not be edited, renamed, moved,
  regenerated, squashed, or deleted.
- `dotnet ef migrations remove` must not be used against a committed migration.
- `ApplicationDbContextModelSnapshot.cs` may change only as the generated
  result of adding a new migration. It must not be edited manually to rewrite
  migration history.
- A newly generated migration must be inspected before acceptance. It may
  contain only the schema changes required by the current feature.
- Applying, dropping, or resetting a database remains a separate destructive
  action and requires explicit user authorization.

## Non-negotiable testing policy

- Do not recreate `AIStudyHub.Tests`.
- Do not create unit-test projects, unit-test files, test fixtures, mocks, test
  packages, or test-only production hooks when adding or fixing a feature.
- Do not add xUnit, NUnit, MSTest, Moq, FluentAssertions, or equivalent unit
  testing dependencies unless the user explicitly reverses this policy.
- Integration or end-to-end test projects may be created only when the user
  explicitly requests them.
- Do not run or require smoke tests.
- The agent may run `dotnet build` to verify compilation.
- Functional verification is performed manually by the repository owner.
- The agent must report which manual flows the owner should verify when handing
  off a feature.

## Documentation changes

### `AGENT.md`

- Add a prominent mandatory workflow section near the coding rules.
- Strengthen the database rules with immutable-migration requirements.
- Add the no-unit-test and owner-manual-testing policy.
- Remove pending or recommended unit-test work that contradicts the policy.

### `ARCHITECTURE.md`

- Add the same repository change policy as an architectural constraint.
- Remove `AIStudyHub.Tests` from the documented solution structure.
- Remove future unit-test recommendations.
- Describe compilation as agent verification and manual functional testing as
  the owner's responsibility.

## Acceptance criteria

- Both documents state the same migration and testing rules.
- Neither document instructs an agent to create unit tests.
- Neither document lists `AIStudyHub.Tests` as an active project.
- Existing migrations are explicitly protected from modification and deletion.
- New migrations remain permitted for approved schema changes.
- Smoke tests are not required or recommended.
