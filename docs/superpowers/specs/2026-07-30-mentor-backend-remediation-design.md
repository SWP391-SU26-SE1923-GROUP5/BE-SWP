# Mentor Backend Remediation Design

**Date:** 2026-07-30

**Status:** Approved in conversation; awaiting written-spec review

## 1. Purpose

This design addresses the backend changes requested during mentor review:

- Remove date of birth from registration.
- Limit uploaded files to exactly 5 MiB.
- Replace hard-coded global Subjects with student-owned Subject CRUD.
- Keep chunking and embedding from delaying or destabilizing upload requests.
- Require an explicit generation count for quizzes and flashcards.
- Add secure quiz history details.
- Add append-only flashcard review history and history details.
- Delete a complete flashcard deck, not only one card.
- Remove citations while retaining document-grounded RAG and PDF page awareness.

The implementation remains backend-only and preserves the existing three-layer
solution:

- `AIStudyHub.API`: HTTP binding, authentication context, and status codes.
- `AIStudyHub.Business`: business rules, ownership, validation, workflows, and
  background processing.
- `AIStudyHub.Data`: EF Core entities, configurations, repositories, and new
  migrations.

No additional project is introduced.

## 2. Repository Constraints

The repository rules in `AGENT.md` and `ARCHITECTURE.md` apply to every delivery
slice:

- New EF Core migrations may be created for approved schema changes.
- Existing migration files are immutable and must not be edited, renamed,
  regenerated, squashed, moved, or deleted.
- `dotnet ef migrations remove` must not be used against a committed migration.
- `ApplicationDbContextModelSnapshot.cs` may change only through generation of a
  new migration.
- New migrations must be inspected before acceptance.
- Migrations, database cleanup, Qdrant cleanup, and file deletion must not be
  applied to an environment without explicit authorization from the repository
  owner.
- Do not recreate `AIStudyHub.Tests`.
- Do not create or run unit tests, integration tests, or smoke tests.
- Agents may run `dotnet build` for compilation verification.
- Functional verification is performed manually by the repository owner using a
  handoff checklist.

## 3. Current-State Findings

### 3.1 Registration

`DateOfBirth` is currently required by `RegisterRequestDtoValidator`, passed
through `AuthService`, and stored on a newly registered `User`. It also exists in
profile and admin contracts and in the `Users.dob` database column.

The mentor requirement is limited to registration. Removing the property from
the entire User domain would be a broader breaking schema change that is not
required.

### 3.2 Upload and processing

The effective upload maximum is currently 50 MiB. The limit is duplicated in
`RagOptions` and `DocumentStorageOptions`, while `DocumentController` reads the
RAG option even though file storage owns the constraint.

The upload endpoint also copies the file through a `MemoryStream`, converts it
to a byte array, and then wraps it in another stream before writing it to disk.
Concurrent uploads therefore create avoidable memory pressure.

Chunking and embedding already execute in `DocumentBackgroundProcessor`, but
the bounded in-memory channel uses `BoundedChannelFullMode.Wait`. Upload can
wait for queue capacity, and queued requests disappear when the process stops.

### 3.3 Subjects

Subject CRUD endpoints already exist, but Subject data is:

- Seeded through `HasData`.
- Shared globally by all users.
- Globally unique by `SubjectCode`.
- Writable only by Admin.
- Missing an owner relationship.

Simply removing the Admin authorization attribute would allow one student to
modify another student's Subject.

### 3.4 Generation counts

Quiz and flashcard generation already accept counts between 1 and 20 in parts
of the service layer. The contracts are inconsistent:

- Quiz uses a lower-cased C# property name.
- Flashcard count defaults to 10 when omitted.
- Validation occurs in different layers.
- Some range and AI-generation failures become HTTP 500.
- Quiz generation does not verify that the caller owns the Document.
- The generators may persist fewer items than requested.

### 3.5 Quiz history

Quiz history summary endpoints exist, but:

- Submission detail returns only summary data.
- One detail path does not enforce submission ownership.
- A Quiz history endpoint can return submissions belonging to other users.
- `DurationSeconds` is accepted by the submit request but is not persisted.
- Submitted answers are stored as JSON keyed by Question ID.

### 3.6 Flashcard history and deck deletion

`FlashcardReview` stores only the latest SM-2 state for one
`(UserId, FlashcardId)` pair. Each review overwrites the previous state, so it
cannot support a real history list or detail view.

The API deletes one Flashcard at a time. There is no Deck entity; in the current
domain, a deck is naturally the complete set of Flashcards belonging to one
Document.

### 3.7 Citations

Citations are implemented across:

- Chat and RAG DTOs.
- RAG prompt construction and orchestration.
- `RagCitationFactory` and `CitationHighlightability`.
- `ChatMessageCitation` persistence.
- Entity configuration and dependency injection.
- Active backend/frontend documentation.

Hiding one response property would leave citation creation and persistence able
to fail the Chat workflow. Removal must therefore be end-to-end.

PDF chunk metadata already contains physical page numbers. That metadata is
useful independently of the citation feature and must remain available to RAG
when a user explicitly asks which page contains information.

## 4. Architectural Boundaries

### 4.1 Thin upload controller

Introduce an `IDocumentUploadService` in the Business layer. The API receives
`IFormFile`, obtains its read stream and metadata, and passes HTTP-neutral input
to the service:

- Authenticated User ID.
- Subject ID.
- Title.
- File name.
- Content type.
- Content length.
- File stream.

The service owns file validation, Subject ownership, storage quota, persistence,
compensation cleanup, and enqueueing. Business code does not depend on
`IFormFile`.

### 4.2 Explicit owned-Subject service

`ISubjectService` no longer inherits generic CRUD. It exposes operations whose
signatures require the authenticated User ID:

- List the current user's Subjects.
- Read one owned Subject.
- Create a Subject for the current user.
- Update an owned Subject.
- Delete an owned Subject.
- Validate an owned Subject for Document creation/upload.

### 4.3 Owner-scoped history

Quiz and flashcard history services take the authenticated User ID as a required
service parameter. Public DTOs never accept a trusted owner User ID.

### 4.4 Separate current state from history

`FlashcardReview` remains the current SM-2 schedule. A new append-only
`FlashcardReviewAttempt` records every state transition.

## 5. Registration Design

`POST /api/Auth/register` accepts:

```json
{
  "fullName": "Nguyen Van A",
  "email": "a@example.com",
  "password": "StrongPassword1!"
}
```

Changes are deliberately limited to registration:

- Remove `DateOfBirth` from `RegisterRequestDto`.
- Remove date-of-birth rules from `RegisterRequestDtoValidator`.
- Remove the date-of-birth argument from the Auth registration workflow.
- Newly registered users retain the entity default of `DateOfBirth = null`.
- Keep `DateOfBirth` in the User entity, profile/admin DTOs, mappings, services,
  and the `Users.dob` column.

This slice requires no migration.

## 6. Student-Owned Subject CRUD

### 6.1 Model

Add required ownership:

```text
Subject
├── Id
├── OwnerUserId
├── SubjectCode
├── SubjectName
├── Description
├── CreatedAt
└── UpdatedAt
```

Add the inverse `User.Subjects` navigation.

The User-to-Subject relationship uses cascade deletion. The
Subject-to-Document relationship remains restrictive, so a Subject cannot be
deleted while referenced by a Document.

The unique index becomes:

```text
(OwnerUserId, SubjectCode)
```

Two students may use the same code. One student may not create the same code
twice.

### 6.2 Behavior

- Owner ID is derived from JWT and is never accepted from the request.
- List returns only the current user's Subjects.
- Detail, update, and delete query by both Subject ID and Owner User ID.
- A missing or foreign Subject returns `404 Not Found`.
- Admin has no cross-user override.
- Subject code is trimmed and normalized to uppercase.
- Duplicate owned code returns `409 Conflict`.
- Deleting a Subject referenced by a Document returns `409 Conflict`.
- Upload and every Document-creation path validate that the selected Subject
  belongs to the current user.

### 6.3 API

Keep the existing routes:

```text
GET    /api/Subject
GET    /api/Subject/{id}
POST   /api/Subject
PUT    /api/Subject/{id}
DELETE /api/Subject/{id}
```

Remove the Subject write endpoints' Admin-only attributes. The controller
retains `[Authorize]`.

### 6.4 Migration

Create `OwnSubjectsAndCleanDemoLearningData`.

The migration:

- Cleans incompatible demo learning data in a safe dependency order.
- Removes the global Subject-code unique index.
- Deletes the Subject seed rows created by historical migrations.
- Adds required `OwnerUserId`.
- Adds the Subject-to-User foreign key.
- Adds the composite unique index.

Remove `HasData` from the current Subject configuration. The historical
`AddHardcodedSubjectsWithDescriptions` migration remains unchanged.

## 7. Demo Learning-Data Cleanup

The ownership conversion intentionally starts learning data from a clean state.
The SQL cleanup removes or resets:

- Subjects and Documents.
- Votes, Reports, DocumentShares, and Document-to-Chat links.
- Quizzes, Questions, Answers, and QuizSubmissions.
- Flashcards and current FlashcardReviews.
- ChatSessions, ChatMessages, and old citation rows.
- StudyLogs, Recommendations, and UserBadges.
- All demo Notifications.
- UserStats learning counters.
- User `CurrentStorageCapacity`.
- All TokenLedgers and User `CurrentAiTokenUsage`.

It preserves:

- Users and Identity roles.
- Tier memberships.
- Payments.
- Authentication records not invalidated by the learning cleanup.

EF migration cleanup covers SQL only. Physical upload files and Qdrant vectors
must be cleared in a coordinated environment-cleanup step after explicit owner
authorization. They must not be deleted automatically while merely generating
or reviewing the migration.

## 8. Upload Limit and Processing Design

### 8.1 One exact limit

`DocumentStorageOptions.MaxFileSizeBytes` is the single source of truth:

```text
5 * 1024 * 1024 = 5,242,880 bytes
```

Remove the duplicate maximum from `RagOptions`.

Configure the multipart body limit to 6 MiB so protocol and form headers fit.
Application validation still enforces the exact 5 MiB file-content maximum. A
file whose content length exceeds 5,242,880 bytes returns
`413 Payload Too Large`.

### 8.2 Validation order

1. Authenticated User.
2. Non-empty file.
3. Non-empty title.
4. Non-empty Subject ID.
5. Subject exists and belongs to the user.
6. File content is no larger than 5 MiB.
7. Extension is allowed.
8. Tier storage quota permits the file.

An empty Subject ID returns 400. A missing or foreign Subject returns 404.

### 8.3 Streamed persistence

Remove the additional byte-array copies. Open the request file stream and pass
it to `IFileStorageService.SaveFileAsync(Stream, ...)`.

After validation:

1. Save the stream to disk.
2. Create a `Document` in `Processing` state.
3. Update storage accounting.
4. Commit database changes.
5. Enqueue processing metadata.
6. Return `202 Accepted`.

If disk persistence succeeds but the database commit fails, delete the newly
saved file. If enqueueing fails after commit, leave the Document in Processing
so startup recovery can pick it up.

Storage-quota evaluation uses byte totals from `Document.FileSizeBytes` rather
than truncating a fractional MiB to an integer. `CurrentStorageCapacity` is set
to the ceiling of the active byte total expressed in MiB for compatibility with
its existing integer contract.

### 8.4 Non-blocking demo queue

The queue uses an unbounded single-reader channel containing only metadata and
never file bytes. Enqueue uses a non-blocking write and does not make upload wait
for extraction, chunking, embedding, or channel capacity.

Within one process, a concurrent set deduplicates queued Document IDs. An ID is
removed after processing succeeds or fails so an explicit reprocess can enqueue
it again. The background worker continues to process one Document at a time,
containing failures per Document.

### 8.5 Startup recovery

At worker startup:

1. Query Documents whose status is Processing.
2. Resolve and validate each source file path.
3. Enqueue valid Documents.
4. Mark a Document Failed with a controlled error if its source file is gone.
5. Consume new queue entries normally.

The existing index-run mechanism protects recovery from stale partial vectors.
A successful recovered run keeps the new run and deletes vectors from previous
or incomplete runs.

No processing-job table or migration is added.

## 9. Exact Quiz and Flashcard Generation Counts

The public JSON contracts are:

```json
{ "numberOfQuestions": 10 }
```

```json
{ "numberOfFlashcards": 10 }
```

Rules:

- Both properties are required.
- Both accept integers from 1 through 20 inclusive.
- Flashcard count no longer defaults to 10.
- C# property names use PascalCase; JSON remains camelCase.
- Missing, zero, negative, fractional, and greater-than-20 values return 400.
- FluentValidation protects the MVC boundary.
- Service guards protect internal callers.

Before generation:

- The Document must exist and belong to the authenticated user.
- A foreign Document returns 404.
- The Document must have status Done; Processing or Failed returns 409.
- The vector store must contain valid Document context.

Generators use bounded retries and persist only when the exact requested count
has been produced. If valid output remains short:

- Do not persist partial Quiz/Question/Answer or Flashcard data.
- Record actual consumed AI tokens.
- Return `422 Unprocessable Entity`.

The Quiz generator gains the ownership check currently missing from its service.

## 10. Quiz History Details

### 10.1 Persistence

Add nullable `DurationSeconds` to `QuizSubmission`.

When provided, duration must be between 1 and 86,400 seconds.

Continue storing submitted answers as JSON keyed by Question ID. A normalized
submission-answer table is not introduced because the current immutable
question/answer structure can support the required detail view without another
relationship.

### 10.2 Summary endpoints

Canonical routes:

```text
GET /api/QuizSubmission/my
GET /api/QuizSubmission/by-quiz/{quizId}
```

Both return only the authenticated user's submissions and include:

- Submission, Quiz, Document, and Subject metadata.
- Score, maximum score, correct count, and percentage.
- Duration.
- Submission and grading timestamps.

The old history endpoint in `QuizController` remains as an owner-scoped
compatibility alias. It must never return submissions from other users.

### 10.3 Detail endpoint

```text
GET /api/QuizSubmission/{submissionId}
```

The service queries by both Submission ID and authenticated User ID. A foreign
submission returns 404.

The detail response contains:

```text
QuizSubmissionDetail
├── submission metadata
├── quiz/document/subject metadata
├── score, maximum score, percentage, duration
└── questions ordered by position
    ├── questionId, title, position, type
    └── options
        ├── answerId
        ├── text
        ├── isSelected
        └── isCorrect
```

Submit validation rejects malformed client answer JSON. History reads parse
persisted JSON defensively. Missing keys represent unanswered questions. Invalid
stored JSON is logged and returned as a stable server error; it must not crash
the process or expose a stack trace.

## 11. Flashcard Review History

### 11.1 Model

Keep `FlashcardReview` as current state and add append-only
`FlashcardReviewAttempt`:

```text
FlashcardReviewAttempt
├── Id
├── UserId
├── FlashcardId
├── Quality
├── TimeSpentSeconds
├── PreviousEaseFactor
├── ResultEaseFactor
├── PreviousInterval
├── ResultInterval
├── PreviousRepetitions
├── ResultRepetitions
├── PreviousNextReviewDate
├── ResultNextReviewDate
├── XpEarned
└── CreatedAt
```

Indexes:

```text
(UserId, CreatedAt)
(UserId, FlashcardId, CreatedAt)
```

User and Flashcard relationships use cascade deletion so attempt history follows
the explicitly approved card/deck deletion semantics.

### 11.2 Review transaction

For each review:

1. Verify that the Flashcard belongs to a Document the user owns, can read
   through an explicit share, or can read because it is public.
2. Capture the previous SM-2 state.
3. Apply SM-2.
4. Update the current `FlashcardReview`.
5. Add one `FlashcardReviewAttempt`.
6. Save the current state and core attempt atomically.
7. Run optional XP and badge hooks.
8. Update attempt `XpEarned` when XP succeeds.

XP or badge failure must not remove the saved schedule or attempt. Optional
`TimeSpentSeconds` uses the 1-to-86,400 range.

### 11.3 History API

```text
GET /api/FlashcardReview/history
GET /api/FlashcardReview/history/{attemptId}
```

The paged list may filter by Document ID, Flashcard ID, and date range. Both
routes derive User ID from JWT.

Detail returns:

- Quality, duration, XP, and review timestamp.
- Complete before/after SM-2 values.
- Flashcard front and back.
- Document ID and title.
- Subject ID, code, and name.

A missing or foreign attempt returns 404.

## 12. Complete Flashcard-Deck Deletion

A deck is every Flashcard with the same Document ID.

Add:

```text
DELETE /api/Flashcard/by-document/{documentId}
```

Rules:

- Only the Document owner may delete the deck.
- A missing or foreign Document returns 404.
- An owned Document with no cards returns 204.
- Delete all cards in one transaction.
- Database cascade deletes current FlashcardReviews and
  FlashcardReviewAttempts.
- Keep the Document, Quizzes, QuizSubmissions, and StudyLogs.

Keep the existing single-card deletion endpoint. It uses the same owner check
and cascades the selected card's current review state and attempt history.

Review history intentionally disappears when its Flashcard or deck is deleted.

## 13. Citation Removal with PDF Page Awareness

### 13.1 Preserve RAG

Keep:

- Extraction, chunking, and embeddings.
- Qdrant vectors and access-filter metadata.
- Hybrid retrieval and reranking.
- Guardrails, confidence, token tracking, and Chat persistence.
- Physical PDF page-number metadata.

### 13.2 Remove citation contracts and persistence

Remove:

- `ChatMessageResponseDto.Citations`.
- Citation collections from internal RAG response types.
- `CitationInfo`.
- `ChatMessageCitation`.
- `ChatMessage.Citations`.
- The citation DbSet and configuration.
- Citation AutoMapper logic.
- `RagCitationFactory`.
- `CitationHighlightability`.
- Citation dependency-injection registration.
- Citation generation, validation, and persistence in `AIChatService`.
- Citation eager loading from Chat history.

Create `RemoveChatCitations` to drop the citation table. The two historical
citation migrations remain unchanged.

### 13.3 Keep non-citation source validation

Replace `RagCitationFactory` with an internal context selector that:

- Rejects results without a valid Document ID.
- Enforces the allowed Document set.
- Rejects blank content.
- Deduplicates the same Document/chunk.

It returns validated SearchResults and does not produce citation DTOs.

### 13.4 Prompt and page questions

Remove:

- Citation markers such as `[1]`.
- "Always cite your sources."
- Citation arrays and source lists.
- Automatic page citations.

Keep page metadata in internal prompt context:

```text
--- DOCUMENT CONTEXT ---
FILE_NAME: report.pdf
PAGE_NUMBER: 7
CONTENT:
...
--- END CONTEXT ---
```

Prompt rules:

- Do not add source markers or a citation list.
- Mention a page number only when the user asks where content appears.
- Use only an explicit page number from chunk metadata.
- Never infer or fabricate a page number.
- If the file or chunk has no page metadata, state that the page cannot be
  determined.

Hybrid-search responses may keep Content, Score, Document ID, File Name, Page
Number, Chunk Index, and Match Type. Remove citation-specific highlightability.

### 13.5 Documentation

Update active documentation:

- `README.md`
- `AGENT.md`
- `ARCHITECTURE.md`
- `docs/FRONTEND_GUIDE.md`

Remove instructions to render citation markers or consume a citation array.
Keep the older persistent-citation design record as history, but mark it clearly
as superseded by this design.

## 14. Error Semantics

Affected endpoints use:

- `200 OK`: successful read or update.
- `201 Created`: Subject creation.
- `202 Accepted`: upload persisted and queued for background processing.
- `204 No Content`: successful deletion, including an empty owned deck.
- `400 Bad Request`: missing or malformed input.
- `401 Unauthorized`: missing or invalid authentication only.
- `404 Not Found`: missing or foreign-owned resource.
- `409 Conflict`: referenced Subject or Document not ready for generation.
- `413 Payload Too Large`: file content exceeds 5 MiB.
- `422 Unprocessable Entity`: AI cannot produce the exact valid count.
- `500 Internal Server Error`: unexpected server or corrupted persisted data.

Known domain failures must be translated consistently. Controllers must not use
broad catch blocks that convert expected range, ownership, or generation errors
to HTTP 500.

## 15. Delivery Slices

Implement in independently buildable vertical slices:

1. Registration contract and 5 MiB streamed/recoverable upload.
2. Student-owned Subject CRUD and demo-data cleanup migration.
3. Required exact Quiz/Flashcard generation counts and Document guards.
4. Quiz history detail and persisted duration.
5. Flashcard attempt history and complete deck deletion.
6. Citation removal with retained PDF page awareness.
7. Active documentation updates.

Schema migrations are separated by domain:

1. `OwnSubjectsAndCleanDemoLearningData`.
2. `AddLearningHistoryDetails`.
3. `RemoveChatCitations`.

Each slice receives its own implementation commit. Migration names are
descriptive and generated once. Existing migrations remain immutable.

## 16. Verification and Handoff

For each slice:

1. Inspect the source diff for scope.
2. Inspect new migration operations and snapshot changes.
3. Confirm no existing migration file changed.
4. Run:

   ```text
   dotnet build AIStudyHub.slnx --no-restore
   ```

5. Report warnings and errors exactly.
6. Provide manual API flows for the repository owner.

Do not create or run unit tests, integration tests, or smoke tests.

The owner manually verifies at least:

- Registration without `dateOfBirth`.
- File acceptance at exactly 5 MiB and rejection one byte above.
- Upload returning 202 before AI processing completes.
- Recovery of a Processing Document after application restart.
- Private per-student Subject CRUD and foreign-resource 404 behavior.
- Referenced-Subject deletion conflict.
- Required generation count boundaries and exact result counts.
- Owner-only Quiz history summary and per-question detail.
- Flashcard attempt list/detail and SM-2 before/after data.
- Whole-deck and single-card deletion behavior.
- Chat and history responses without citation fields or markers.
- RAG answers still grounded in attached Documents.
- PDF page questions still answered from real page metadata.

Applying migrations and coordinated demo-environment cleanup remain separate
owner-authorized operations.

## 17. Acceptance Criteria

The remediation is complete when:

- Register accepts no `dateOfBirth` property and the rest of the User domain
  still supports profile/admin date of birth.
- Files larger than 5,242,880 bytes receive 413.
- Upload does not hold duplicate whole-file buffers or wait for AI work.
- Processing Documents recover after a process restart without a new job table.
- Students privately CRUD non-seeded Subjects.
- Subject ownership is enforced in every Document-selection path.
- Quiz and flashcard generation require integers from 1 through 20 and persist
  only exact counts.
- Quiz history detail shows each option, the selected answer, the correct answer,
  result, and duration for the submission owner.
- Every flashcard review appends an attempt with complete SM-2 before/after
  state.
- A Document owner can delete all Flashcards for that Document in one request.
- Chat and RAG contain no citation marker contract, citation DTO, or citation
  persistence.
- RAG retains real PDF page awareness for explicit page-location questions.
- Active documentation matches the final contracts.
- Existing migration files remain unchanged.
- Every slice builds successfully and is handed off with manual verification
  steps.
