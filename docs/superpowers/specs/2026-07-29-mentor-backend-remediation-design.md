# Mentor Backend Remediation Design

**Date:** 2026-07-29

**Status:** Approved in conversation

## 1. Purpose

This design addresses the backend changes requested during mentor review:

- Remove date of birth from registration and user contracts.
- Limit uploaded files to 5 MiB.
- Replace hard-coded global subjects with student-owned Subject CRUD.
- Ensure document upload is not blocked by embedding and chunking.
- Require an explicit generation count for quizzes and flashcards.
- Add quiz history details.
- Add flashcard review history and history details.
- Delete a complete flashcard deck by document.
- Remove citations while retaining document-grounded RAG chat.

The repository remains an ASP.NET Core 8 three-layer application:

- `AIStudyHub.API`: HTTP endpoints and authentication context.
- `AIStudyHub.Business`: business rules, DTOs, validation, and workers.
- `AIStudyHub.Data`: EF Core entities, configurations, repositories, and migrations.

## 2. Scope Boundaries

### In scope

- Backend API, business logic, persistence, migrations, tests, and backend-facing documentation.
- A durable database-backed document-processing queue.
- Resetting the development/demo database after the schema changes.
- Adding new migrations while retaining every existing migration file.

### Out of scope

- Frontend implementation.
- Production data conversion from global subjects to student-owned subjects.
- Large-scale concurrent upload benchmarking and infrastructure tuning.
- Removing RAG retrieval or document-grounded chat.
- Deleting or rewriting existing migration files.

The development/demo database may be dropped and recreated. Uploaded demo files and Qdrant demo vectors may also be cleared as an environment reset, but destructive environment operations are not part of an EF migration.

## 3. Current-State Findings

### User date of birth

`DateOfBirth` currently appears in the `User` entity, EF configuration, authentication registration DTO and validator, user request/response DTOs, mappings, and Auth/User services. A schema migration is required to remove the `dob` column.

### Upload limit and document processing

The current maximum is 50 MiB and is declared in both `RagOptions` and `DocumentStorageOptions`. `DocumentController` checks the RAG option even though file storage owns this constraint.

Upload already enqueues extraction, chunking, and embedding for a background worker. However, the queue is an in-memory bounded channel configured with `BoundedChannelFullMode.Wait`. Upload can therefore wait when the queue is full, and queued work is lost on application restart.

### Subjects

Subject endpoints already expose CRUD, but writes are Admin-only. Subjects are seeded through `HasData`, globally unique by code, and have no owner. Documents from all users can reference the same Subject. Simply removing the Admin attribute would permit cross-user modification.

### Generation counts

Quiz and flashcard generation services already accept counts from 1 through 20. Flashcards default to 10 when the property is omitted, and invalid flashcard counts can pass through a broad controller catch and become HTTP 500.

### Quiz history

Quiz history list endpoints exist. The current detail endpoint returns only submission summary fields and does not enforce submission ownership. The request accepts `DurationSeconds`, but the entity does not persist it. Submitted answers are stored as a JSON dictionary keyed by question ID.

### Flashcard history

`FlashcardReview` stores only the latest SM-2 state for a `(UserId, FlashcardId)` pair. Each review overwrites that state. `StudyLog` is append-only but does not contain `FlashcardId`, review quality, or SM-2 before/after values, so it cannot provide review history details.

### Flashcard deck deletion

The API deletes one Flashcard at a time. In this model, a deck is the complete set of Flashcards belonging to one Document.

### Citations

Citations are fully implemented across RAG orchestration, chat DTOs, persisted `ChatMessageCitation` rows, migrations, mappings, tests, and documentation. Removal therefore requires a complete contract and persistence cleanup rather than hiding one response property.

## 4. Architectural Decisions

### 4.1 Remove date of birth completely

Remove `DateOfBirth` from all public and internal user models:

- Registration request and validation.
- User create, update, patch, profile, and response DTOs.
- `User` entity and EF configuration.
- Auth and User services.
- AutoMapper configuration.

A new migration drops `Users.dob`. Existing migrations remain unchanged. After database reset, EF applies the historical migrations and then the new migration, producing the intended final schema.

### 4.2 Make subjects private student resources

Add required `OwnerUserId` to `Subject`, with a `User` navigation and a `User.Subjects` collection.

Subject codes are unique per owner through a composite unique index:

```text
(OwnerUserId, SubjectCode)
```

The API never accepts an owner ID from the request. It derives ownership from the authenticated JWT.

All Subject operations use these rules:

- List returns only the current user's subjects.
- Detail, update, and delete query by both Subject ID and owner ID.
- Create assigns the current user as owner.
- Admin receives no cross-user override.
- A resource owned by another user is returned as `404 Not Found`, preventing ownership disclosure.
- Deleting a Subject referenced by any Document returns `409 Conflict`.

Remove Subject `HasData` seeding. The demo database is reset rather than converting existing shared Subject and Document rows.

Document creation and updates must verify that the selected Subject belongs to the current user.

### 4.3 Use one exact 5 MiB upload limit

`DocumentStorageOptions.MaxFileSizeBytes` is the single source of truth. Its configured and default value is:

```text
5 * 1024 * 1024 = 5,242,880 bytes
```

Remove the duplicate maximum from `RagOptions`.

The multipart request limit permits protocol overhead, while application validation enforces the exact file-content limit. A file larger than 5,242,880 bytes returns `413 Payload Too Large`.

Upload validation order is:

1. Authenticated user.
2. Non-empty file.
3. Non-empty title.
4. Non-empty Subject ID.
5. Subject exists and belongs to the user.
6. File content is at most 5 MiB.
7. Extension is supported.
8. The user's storage quota permits the file.

An empty Subject ID returns `400 Bad Request`. A missing or foreign-owned Subject returns `404 Not Found`.

### 4.4 Decouple upload with durable processing jobs

Add a `DocumentProcessingJob` entity rather than relying on the in-memory channel as the source of truth.

The job contains:

- `DocumentId`.
- `Status`: `Queued`, `Processing`, `Completed`, or `Failed`.
- `AttemptCount`.
- `NextAttemptAt`.
- `ClaimId`.
- `ClaimedAt`.
- `LastError`.
- Standard creation and update timestamps.

Only one active processing job exists per Document.

The upload flow is:

1. Validate the request.
2. Save the file.
3. Create the Document in `Processing` state.
4. Create its `Queued` processing job.
5. Commit the Document, user storage update, and job in one database transaction.
6. Return `202 Accepted` without waiting for extraction, chunking, embedding, or queue capacity.

The worker polls for eligible jobs, atomically claims one, creates a dependency-injection scope, and performs extraction, chunking, embedding, suggested-prompt generation, and status updates.

On success:

- Document becomes `Done`.
- Job becomes `Completed`.
- Claim and previous error fields are cleared.

On failure:

- The exception is contained within the worker.
- Document exposes the processing failure through its status and error field.
- Retryable jobs receive an incremented attempt count and exponential backoff.
- A job becomes `Failed` after the configured maximum attempts.

Claims have a timeout so work abandoned by a stopped process becomes eligible again. This preserves work across application restarts and prevents embedding/chunking throughput from blocking upload requests.

The existing reprocessing endpoint creates or resets a processing job rather than writing directly to an in-memory queue.

### 4.5 Require explicit generation counts

Both request contracts require an integer:

- `NumberOfQuestions`: 1 through 20 inclusive.
- `NumberOfFlashcards`: 1 through 20 inclusive.

`NumberOfFlashcards` no longer has a default. C# properties use PascalCase; the configured ASP.NET JSON policy exposes them as `numberOfQuestions` and `numberOfFlashcards`.

FluentValidation rejects a missing value, zero, negative values, and values greater than 20 with HTTP 400. Service-layer guards remain in place for callers that bypass MVC validation. Controllers map service range errors to HTTP 400 rather than HTTP 500.

### 4.6 Return secure quiz history details

Keep the existing paged quiz-history list. Persist `DurationSeconds` on `QuizSubmission`, because the request already accepts it and history currently always returns null.

The authenticated submission-detail contract returns:

- Submission ID and timestamps.
- Quiz, Document, and Subject metadata.
- Score, maximum score, correct count, percentage, and duration.
- Questions ordered by position.
- Every available answer option.
- The option submitted by the user.
- The correct option.
- Whether the submitted option was correct.

The service loads a submission only when `submission.UserId` matches the current user. Foreign submissions return `404 Not Found`.

The existing answers JSON is parsed defensively. Missing answers are represented as unanswered. Malformed persisted JSON produces a controlled domain error and never crashes the API process.

Quiz list endpoints remain summary-oriented; the detail endpoint owns the expanded question result.

### 4.7 Persist flashcard review attempts

Add an append-only `FlashcardReviewAttempt` entity. `FlashcardReview` remains the current SM-2 schedule, while attempts provide audit/history data.

Each attempt stores:

- `UserId`.
- `FlashcardId`.
- `Quality`.
- `TimeSpentSeconds`.
- Previous and resulting ease factor.
- Previous and resulting interval.
- Previous and resulting repetitions.
- Resulting next-review date.
- XP earned.
- Review timestamp.

Updating the current SM-2 state and inserting the core attempt record occur in one transaction. If an optional downstream XP or badge operation fails, the review and attempt remain valid. The attempt's XP value is updated when XP award succeeds.

Expose:

```text
GET /api/FlashcardReview/history
GET /api/FlashcardReview/history/{attemptId}
```

The list is paged and may filter by Document ID, Flashcard ID, and date range. Detail includes the flashcard front/back plus Document and Subject metadata.

Both endpoints derive the user from JWT and return only that user's attempts. A foreign attempt returns `404 Not Found`.

### 4.8 Delete a complete flashcard deck

A flashcard deck is all Flashcards belonging to a Document.

Add an owner-only bulk-delete endpoint under the Flashcard resource, keyed by Document ID. It deletes, in one transaction:

1. Related `FlashcardReviewAttempt` rows.
2. Related current `FlashcardReview` rows.
3. All Flashcards for the Document.

It does not delete the Document, Quiz, or unrelated study data. An existing owned Document with no Flashcards returns `204 No Content`. A missing or foreign Document returns `404 Not Found`.

The existing single-card delete endpoint remains available and must also delete its dependent review state and attempts safely.

### 4.9 Remove citations but retain RAG

RAG retrieval, reranking, grounding, chat, confidence calculation, and message persistence remain.

Remove citation behavior from the public and internal contract:

- The LLM prompt no longer requests `[1]`, `[2]`, or other citation markers.
- RAG orchestration responses contain the answer, confidence, usage, and relevance only.
- Chat message responses no longer expose a citations collection.
- Assistant messages no longer persist citation snapshots.

Remove the citation-specific implementation:

- `ChatMessageCitation` entity.
- Citation navigation from `ChatMessage`.
- Citation DbSet and EF configuration.
- `CitationInfo`.
- `RagCitationFactory`.
- `CitationHighlightability`.
- Citation-specific mapping and dependency injection.
- Citation-specific tests.

Refactor the prompt-context builder to concatenate safe retrieved text without producing citation labels or page-citation instructions.

A new migration drops `ChatMessageCitation`. The two existing citation migrations remain in the repository. Resetting the demo database therefore creates the historical table and later removes it, leaving the correct final schema.

Update current README, architecture, API contract, and frontend-integration guidance so they no longer claim that citations are returned. Historical design records may remain only when clearly identified as superseded; active documentation must not instruct clients to consume citation fields.

## 5. API and Error Semantics

The affected endpoints use consistent outcomes:

- `200 OK`: successful read or update.
- `201 Created`: Subject creation.
- `202 Accepted`: upload accepted and durable processing job created.
- `204 No Content`: successful deletion, including an empty owned flashcard deck.
- `400 Bad Request`: malformed input, empty Subject ID, invalid generation count, or malformed client answers.
- `401 Unauthorized`: missing or invalid authentication.
- `404 Not Found`: missing resource or a resource owned by another user.
- `409 Conflict`: deletion of a Subject that is referenced by Documents.
- `413 Payload Too Large`: file content exceeds 5 MiB.
- `500 Internal Server Error`: unexpected server failure only.

Global exception handling should translate known validation and business exceptions consistently instead of relying on broad controller catches.

## 6. Data Integrity and Security

- Ownership is always derived from authenticated claims.
- No DTO accepts a trusted `OwnerUserId`.
- Subject ownership is enforced in service queries, not only controllers.
- Quiz and flashcard history endpoints never accept another user's ID as an authorization mechanism.
- Document processing jobs use atomic claims to avoid duplicate active workers.
- Subject deletion uses a precondition check and a restrictive database relationship.
- Flashcard deck deletion removes dependent state in one transaction.
- Citations are removed from both runtime contracts and persistence so stale snapshots cannot remain exposed.
- Existing migration files are immutable.

## 7. Testing Strategy

Implementation follows test-driven development. Each delivery slice adds focused service, validator, controller-contract, and persistence tests.

### Date of birth

- Registration succeeds without a date of birth.
- User contracts and Swagger contain no date-of-birth property.
- The final EF model contains no `dob` column.

### Subject ownership

- Users see only their own subjects.
- Two users may use the same Subject code.
- A user cannot read, update, delete, or select another user's Subject.
- Deleting a referenced Subject returns conflict.
- Subject seed data is absent from the final model.

### Upload and processing

- Files of exactly 5,242,880 bytes are accepted.
- Files one byte larger are rejected with 413.
- Empty and foreign Subject IDs are rejected correctly.
- Upload returns 202 before extraction/embedding completes.
- A queued job survives worker recreation.
- Only one worker can claim a job.
- Failed jobs retry and eventually become failed.
- Processing failure does not remove the accepted Document.

### Generation counts

- Values 1 and 20 succeed validation.
- Missing, 0, negative, and 21 fail with 400.
- Invalid service-level requests do not become 500.

### Quiz history

- Only the submission owner can read detail.
- Correct, incorrect, and unanswered questions are represented correctly.
- Duration is persisted and returned.
- Malformed stored answer JSON is handled without an unhandled exception.

### Flashcard history and deletion

- Each review appends one attempt while updating one current schedule row.
- List pagination and filters are user-scoped.
- Detail includes before/after SM-2 state and card metadata.
- Foreign history is not visible.
- Bulk deletion removes cards, schedules, and attempts only.
- Deleting an empty owned deck is idempotent.

### Citation removal

- Chat and RAG contracts contain no citations field.
- Assistant prompts contain no citation-marker instruction.
- Messages still persist and reload.
- Grounded RAG answers still use retrieved document content.
- The final EF model contains no `ChatMessageCitation` table.
- Repository search finds no active citation code outside preserved historical migrations and explicitly superseded historical documentation.

### Regression

- Build the complete solution.
- Run the complete automated test suite.
- Recreate the demo database from the full migration chain.
- Exercise the changed endpoints through integration tests or Swagger.

The pre-change baseline is 198 passing tests.

## 8. Delivery Slices

Implementation is divided into independently reviewable slices:

1. Remove date of birth and add the schema migration.
2. Convert Subject to private student-owned CRUD.
3. Enforce the 5 MiB limit and introduce durable document-processing jobs.
4. Require and validate quiz/flashcard generation counts.
5. Add secure quiz history details and persist duration.
6. Add flashcard review attempts, history list, and detail.
7. Add complete flashcard-deck deletion.
8. Remove citations while retaining RAG behavior.
9. Run full regression, recreate the demo database, and update active documentation.

Each slice must leave the solution buildable and its affected tests passing. Schema-dependent slices each add a new migration; no existing migration is modified or deleted.

## 9. Acceptance Criteria

The work is complete when:

- Registration and all active user contracts contain no date of birth.
- Backend rejects file content above 5 MiB.
- Students privately CRUD their own non-seeded Subjects.
- Referenced Subjects cannot be deleted.
- Upload returns after durable acceptance, independent of chunking and embedding execution time.
- Quiz and flashcard generation require counts from 1 through 20.
- A student can view summary and per-question details for their own quiz attempts.
- A student can view list and detail history for every flashcard review attempt.
- A student can delete all Flashcards for an owned Document in one operation.
- Chat and RAG continue to work without citation markers, citation DTOs, or citation persistence.
- Existing migration files remain present and unchanged.
- The demo database recreates successfully from the complete migration chain.
- The complete automated test suite passes.
