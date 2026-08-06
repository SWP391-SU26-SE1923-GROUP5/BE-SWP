# Remove Citations While Keeping Page-Aware RAG Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove citation DTOs, persistence, highlighting metadata, and response arrays end-to-end while preserving secure RAG retrieval and the ability to answer a user's explicit page-number question when trustworthy page metadata exists.

**Architecture:** Replace the citation factory with a neutral `RagContextSelector` that retains its important document-allowlist validation and chunk deduplication. RAG responses contain only answer, confidence, relevance, and optional token usage. `pageNumber` remains internal chunk metadata and appears only in the LLM context; it is never returned as a citation object. Remove the obsolete chat-citation table through one new migration.

**Tech Stack:** ASP.NET Core 8, C# 12, EF Core 8, SQL Server, OpenAI, Qdrant.

## Global Constraints

- Do not recreate `AIStudyHub.Tests` or create any test project/file.
- Do not run unit tests, integration tests, or smoke tests.
- Use `dotnet build AIStudyHub.slnx --no-restore` for agent verification.
- The repository owner performs all functional verification manually.
- A new migration is allowed, but every existing migration is immutable.
- Never edit an existing migration or run `dotnet ef migrations remove` against a committed migration.
- Do not apply the new migration without separate explicit authorization.
- Keep RAG retrieval, document authorization, reranking, relevance checks, guardrails, confidence, and token tracking.
- Keep internal `pageNumber` and `chunkIndex` metadata.
- Never invent, estimate, or infer a page number from chunk order or content.
- Do not return citation markers, citation arrays, source snippets, or highlight flags in chat API responses.

---

## File Structure

### Neutral RAG context selection

- Create `AIStudyHub.Business/AI/Orchestration/RagContextSelector.cs`.
- Delete `AIStudyHub.Business/AI/Orchestration/RagCitationFactory.cs`.
- Delete `AIStudyHub.Business/AI/Orchestration/CitationHighlightability.cs`.
- Modify `AIStudyHub.Business/AI/Orchestration/RagPromptContextBuilder.cs`.
- Modify `AIStudyHub.Business/AI/Orchestration/SemanticKernelOrchestrator.cs`.
- Modify `AIStudyHub.Business/Interfaces/AI/Orchestration/OrchestrationTypes.cs`.
- Modify `AIStudyHub.Business/Services/BusinessServiceExtensions.cs`.
- Modify `AIStudyHub.Business/DTOs/Rag/HybridSearchDtos.cs`.
- Modify `AIStudyHub.Business/DTOs/Documents/ExtractedTextSegment.cs`.
- Modify `AIStudyHub.Business/DTOs/Documents/DocumentChunkDto.cs`.
- Modify `AIStudyHub.Business/Services/DocumentProcessingService.cs`.
- Modify `AIStudyHub.Business/Services/DocumentChunkAssembler.cs`.
- Modify `AIStudyHub.Business/Workers/DocumentBackgroundProcessor.cs`.

### Chat contract and persistence

- Modify `AIStudyHub.Business/DTOs/AIChat/ChatDtos.cs`.
- Modify `AIStudyHub.Business/AI/Chat/AIChatService.cs`.
- Modify `AIStudyHub.Business/Mappings/ApplicationMappingProfile.cs`.
- Modify `AIStudyHub.Data/Entities/ChatMessage.cs`.
- Delete `AIStudyHub.Data/Entities/ChatMessageCitation.cs`.
- Modify `AIStudyHub.Data/ApplicationDbContext.cs`.
- Modify `AIStudyHub.Data/Configurations/EntityConfigurations.cs`.

### Migration and documentation

- Generate a new `RemoveChatCitations` migration under `AIStudyHub.Data/Migrations/`.
- Allow EF Core to update `AIStudyHub.Data/Migrations/ApplicationDbContextModelSnapshot.cs`.
- Modify `docs/api-contract.md`.
- Modify `docs/backend-feature-status.md`.
- Modify `README.md`, `AGENT.md`, `ARCHITECTURE.md`, and `docs/FRONTEND_GUIDE.md`.
- Modify `docs/superpowers/specs/2026-07-16-persistent-chat-citations-design.md` only to add a superseded notice that links to the approved remediation design.
- Modify `AIStudyHub.API/AIStudyHub.API.http`.

---

## Task 1: Replace Citation Construction With Neutral Context Selection

**Files:**

- Create: `AIStudyHub.Business/AI/Orchestration/RagContextSelector.cs`
- Delete: `AIStudyHub.Business/AI/Orchestration/RagCitationFactory.cs`
- Modify: `AIStudyHub.Business/Services/BusinessServiceExtensions.cs`

- [ ] **Step 1: Preserve the security and quality behavior**

Create:

```csharp
public sealed record RagContextSource(
    SearchResult Result,
    Guid DocumentId,
    int? PageNumber,
    int? ChunkIndex);

public sealed class RagContextSelector
{
    public IReadOnlyList<RagContextSource> Select(
        IReadOnlyList<SearchResult> results,
        IReadOnlyCollection<Guid>? allowedDocumentIds);
}
```

Move these behaviors from `RagCitationFactory.Create` into `Select`:

- reject missing/invalid/empty `documentId`;
- reject a result outside a supplied Document allowlist;
- reject blank source/content;
- parse optional positive `pageNumber` and non-negative `chunkIndex`; treat invalid
  values as absent;
- deduplicate by `(DocumentId, ChunkIndex)` when chunk index exists;
- otherwise deduplicate by `(DocumentId, PageNumber, Content)`;
- preserve retrieval order;
- log rejected metadata without logging the full chunk content.

Do not create citation indexes, snippets, highlight state, or a public-source DTO.

- [ ] **Step 2: Update dependency injection**

Replace:

```csharp
services.AddScoped<RagCitationFactory>();
```

with:

```csharp
services.AddScoped<RagContextSelector>();
```

- [ ] **Step 3: Delete obsolete citation helpers**

Delete `RagCitationFactory.cs` after all behavior above is represented in the selector. Do not delete `CitationHighlightability.cs` until Task 4 has removed its other consumer.

- [ ] **Step 4: Build and commit**

After temporarily changing the orchestrator constructor/type reference enough to compile in Task 2, commit Tasks 1 and 2 together. Do not make an intermediate commit that intentionally breaks the solution.

---

## Task 2: Simplify Internal RAG Responses and Orchestration

**Files:**

- Modify: `AIStudyHub.Business/Interfaces/AI/Orchestration/OrchestrationTypes.cs`
- Modify: `AIStudyHub.Business/AI/Orchestration/SemanticKernelOrchestrator.cs`
- Modify: `AIStudyHub.Business/AI/Orchestration/RagPromptContextBuilder.cs`

- [ ] **Step 1: Remove citation types from the internal contract**

Change the records to:

```csharp
public sealed record RagResponse(
    string Answer,
    double Confidence,
    bool IsRelevant);

public sealed record RagResponseWithUsage(
    string Answer,
    double Confidence,
    int InputTokens,
    int OutputTokens,
    bool IsRelevant);
```

Delete `CitationInfo`. Keep `SummarizeResult` unchanged.

- [ ] **Step 2: Use selected contexts in both Ask methods**

Replace the factory dependency with `RagContextSelector`. In both `AskAsync` and `AskWithTrackingAsync`:

```csharp
var candidates = (await _retrievalPipeline
    .RetrieveAsync(question, userId, documentIds, ct))
    .ToList();
var contexts = _contextSelector.Select(candidates, documentIds);
var resultList = contexts.Select(context => context.Result).ToList();
```

Use `resultList` for relevance, yes/no detection, and faithfulness exactly as
today. Pass `contexts` to `RagPromptContextBuilder.Build`. Update every response
constructor for the smaller record shapes. Log `valid context chunks`, not
`valid citation sources`.

- [ ] **Step 3: Keep trustworthy page metadata in the prompt context**

Change `RagPromptContextBuilder.Build` to accept
`IEnumerable<RagContextSource>` so it consumes the selector's already validated
metadata instead of parsing raw metadata again. Its output uses neutral fields:

```text
--- DOCUMENT CONTEXT ---
FILE_NAME: <file name>
PAGE_NUMBER: <positive integer>
CONTENT:
<chunk text>
--- END CONTEXT ---
```

When `RagContextSource.PageNumber` is absent, emit:

```text
PAGE_NUMBER: unknown
```

Do not emit `AUTHORITATIVE_CITATION_PAGE`, `PAGE_CITATION_AVAILABLE`, citation indexes, or frontend-oriented metadata.

- [ ] **Step 4: Correct page-answering instructions**

Use one shared system-prompt constant/helper for both Ask methods so the contracts cannot drift. It must say:

```text
- Answer from the supplied source content only.
- Mention a document/page only when the user explicitly asks where the content appears.
- Use PAGE_NUMBER only when it is a positive integer.
- If every supporting chunk has PAGE_NUMBER: unknown, state that the exact page is unavailable.
- Never infer a page from chunk order, surrounding text, document length, or model knowledge.
- Never output metadata labels, bracketed source markers, a source list, or a citation section.
```

Remove the current default instruction to mention document/page on every answer and remove wording that requires answers to end with citations.

- [ ] **Step 5: Preserve format-specific behavior**

The context builder consumes existing metadata:

- PDF chunks with reliable metadata may expose the physical PDF page.
- DOCX/TXT and legacy chunks without page metadata expose `unknown`.
- OCR chunks use a page only when the extraction pipeline already attached a positive physical page number.

This task must not reinterpret `chunkIndex` as a page.

- [ ] **Step 6: Build and commit Tasks 1–2**

```powershell
dotnet build AIStudyHub.slnx --no-restore
git add AIStudyHub.Business/AI/Orchestration/RagContextSelector.cs AIStudyHub.Business/AI/Orchestration/RagCitationFactory.cs AIStudyHub.Business/AI/Orchestration/RagPromptContextBuilder.cs AIStudyHub.Business/AI/Orchestration/SemanticKernelOrchestrator.cs AIStudyHub.Business/Interfaces/AI/Orchestration/OrchestrationTypes.cs AIStudyHub.Business/Services/BusinessServiceExtensions.cs
git diff --cached --check
git commit -m "refactor: remove citations from rag orchestration"
```

Expected: the deleted factory appears as `D`, the selector appears as `A`, and the solution builds.

---

## Task 3: Remove Citations From Chat API and Persistence

**Files:**

- Modify: `AIStudyHub.Business/DTOs/AIChat/ChatDtos.cs`
- Modify: `AIStudyHub.Business/AI/Chat/AIChatService.cs`
- Modify: `AIStudyHub.Business/Mappings/ApplicationMappingProfile.cs`
- Modify: `AIStudyHub.Data/Entities/ChatMessage.cs`
- Delete: `AIStudyHub.Data/Entities/ChatMessageCitation.cs`
- Modify: `AIStudyHub.Data/ApplicationDbContext.cs`
- Modify: `AIStudyHub.Data/Configurations/EntityConfigurations.cs`

- [ ] **Step 1: Shrink the public chat message contract**

Change:

```csharp
public sealed record ChatMessageResponseDto(
    Guid Id,
    Guid ChatSessionId,
    string Sender,
    string Content,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    bool IsRelevant);
```

Delete `ChatCitationDto`. This is a deliberate breaking API change: existing clients must stop reading `citations`.

- [ ] **Step 2: Stop loading and mapping citation rows**

In `AIChatService`:

- remove `.Include(message => message.Citations)`;
- remove local citation collections;
- read only `Answer`, `Confidence`, `IsRelevant`, and token usage from the orchestrator;
- create `ChatMessage` without a citation navigation initializer;
- leave chat token accounting and message persistence unchanged.

In `ApplicationMappingProfile`, replace citation-specific custom maps/ignored members with the normal `ChatMessage -> ChatMessageResponseDto` field mapping.

- [ ] **Step 3: Remove the persistence model**

- remove `ChatMessage.Citations`;
- delete `ChatMessageCitation.cs`;
- remove `ApplicationDbContext.ChatMessageCitations`;
- delete the entire `ChatMessageCitationConfiguration`.

Do not modify `ChatMessage.Content`, `IsRelevant`, sessions, session-document links, or message retention.

- [ ] **Step 4: Build and commit**

```powershell
dotnet build AIStudyHub.slnx --no-restore
git add AIStudyHub.Business/DTOs/AIChat/ChatDtos.cs AIStudyHub.Business/AI/Chat/AIChatService.cs AIStudyHub.Business/Mappings/ApplicationMappingProfile.cs AIStudyHub.Data/Entities/ChatMessage.cs AIStudyHub.Data/Entities/ChatMessageCitation.cs AIStudyHub.Data/ApplicationDbContext.cs AIStudyHub.Data/Configurations/EntityConfigurations.cs
git diff --cached --check
git commit -m "refactor: remove persisted chat citations"
```

Expected: history and newly created messages serialize with no `citations` property.

---

## Task 4: Remove Highlightability From Hybrid Search

**Files:**

- Modify: `AIStudyHub.Business/DTOs/Rag/HybridSearchDtos.cs`
- Modify: `AIStudyHub.Business/DTOs/Documents/ExtractedTextSegment.cs`
- Modify: `AIStudyHub.Business/DTOs/Documents/DocumentChunkDto.cs`
- Modify: `AIStudyHub.Business/Services/DocumentProcessingService.cs`
- Modify: `AIStudyHub.Business/Services/DocumentChunkAssembler.cs`
- Modify: `AIStudyHub.Business/Workers/DocumentBackgroundProcessor.cs`
- Delete: `AIStudyHub.Business/AI/Orchestration/CitationHighlightability.cs`

- [ ] **Step 1: Keep diagnostic search data but remove citation UI state**

Change `HybridSearchResultDto` to keep:

```text
Content
Score
DocumentId
FileName
PageNumber
ChunkIndex
MatchType
```

Remove `IsHighlightable`. In `TryFromSearchResult`, retain Document-ID validation and integer parsing, but remove the highlightability helper call.

Hybrid search remains a diagnostic/search contract and may still expose existing page/chunk metadata. It must not return a citation index or source-highlight classification.

- [ ] **Step 2: Remove highlightability from ingestion metadata**

Remove `IsHighlightable` from `ExtractedTextSegment` and `DocumentChunkDto`.
Update all constructors and grouping logic accordingly:

- group extracted content by `ContentType` and `PageNumber`;
- keep `ContentType` because it remains useful provenance;
- keep `PageNumber`;
- remove the Qdrant payload key `isHighlightable`;
- do not rewrite or delete existing vectors merely for this metadata removal.

Newly processed/reprocessed Documents stop writing the obsolete flag. Existing
vectors may retain an unused key until the user explicitly reprocesses or clears
them.

- [ ] **Step 3: Delete the final obsolete helper**

Run:

```powershell
rg -n "CitationHighlightability" AIStudyHub.API AIStudyHub.Business AIStudyHub.Data --glob "!AIStudyHub.Data/Migrations/**"
```

Expected before deletion: only the helper declaration remains. Then delete `CitationHighlightability.cs`.

- [ ] **Step 4: Build and commit**

```powershell
dotnet build AIStudyHub.slnx --no-restore
git add AIStudyHub.Business/DTOs/Rag/HybridSearchDtos.cs AIStudyHub.Business/DTOs/Documents/ExtractedTextSegment.cs AIStudyHub.Business/DTOs/Documents/DocumentChunkDto.cs AIStudyHub.Business/Services/DocumentProcessingService.cs AIStudyHub.Business/Services/DocumentChunkAssembler.cs AIStudyHub.Business/Workers/DocumentBackgroundProcessor.cs AIStudyHub.Business/AI/Orchestration/CitationHighlightability.cs
git diff --cached --check
git commit -m "refactor: remove citation highlight metadata"
```

---

## Task 5: Generate the Citation-Table Removal Migration

**Files:**

- Create: `AIStudyHub.Data/Migrations/<timestamp>_RemoveChatCitations.cs`
- Create: `AIStudyHub.Data/Migrations/<timestamp>_RemoveChatCitations.Designer.cs`
- Modify: `AIStudyHub.Data/Migrations/ApplicationDbContextModelSnapshot.cs`

- [ ] **Step 1: Verify migration immutability before generation**

Run:

```powershell
git status --short AIStudyHub.Data/Migrations
```

Expected: no migration changes.

- [ ] **Step 2: Generate the new migration**

Use the repository's required design-time environment values, then run:

```powershell
dotnet ef migrations add RemoveChatCitations --project AIStudyHub.Data/AIStudyHub.Data.csproj --startup-project AIStudyHub.API/AIStudyHub.API.csproj
```

Do not run `database update`.

- [ ] **Step 3: Inspect the generated operations**

The `Up` method must drop only `ChatMessageCitation`. The `Down` method may recreate only the schema owned by this new migration. Verify no other table/column/index is changed and no previous timestamped migration is modified.

Dropping this table intentionally removes old citation snapshots. It does not remove `ChatMessage` rows or their text.

- [ ] **Step 4: Verify model state**

Run:

```powershell
dotnet build AIStudyHub.slnx --no-restore
dotnet ef migrations has-pending-model-changes --project AIStudyHub.Data/AIStudyHub.Data.csproj --startup-project AIStudyHub.API/AIStudyHub.API.csproj
```

Expected: build succeeds and EF reports no pending model changes. Do not apply the migration.

- [ ] **Step 5: Commit the generated migration**

```powershell
git add AIStudyHub.Data/Migrations
git diff --cached --name-status
git diff --cached --check
git commit -m "refactor: remove chat citation schema"
```

Stop before committing if Git reports any existing timestamped migration as modified/deleted.

---

## Task 6: Update Active Contracts and Audit Removal

**Files:**

- Modify: `docs/api-contract.md`
- Modify: `docs/backend-feature-status.md`
- Modify: `README.md`
- Modify: `AGENT.md`
- Modify: `ARCHITECTURE.md`
- Modify: `docs/FRONTEND_GUIDE.md`
- Modify: `docs/superpowers/specs/2026-07-16-persistent-chat-citations-design.md`
- Modify: `AIStudyHub.API/AIStudyHub.API.http`

- [ ] **Step 1: Update active API documentation**

Document that:

- chat responses no longer include `citations`;
- normal answers do not automatically name documents/pages;
- when a user explicitly asks for a page, PDF/OCR answers may state a page only from positive `pageNumber` metadata;
- DOCX/TXT/legacy content without page metadata returns an honest “exact page unavailable” statement;
- hybrid search retains `PageNumber`/`ChunkIndex` but removes `IsHighlightable`;
- existing stored chat text remains after the migration.

At the top of the older persistent-citation design, add a short
`Superseded` notice linking to
`2026-07-30-mentor-backend-remediation-design.md`. Do not rewrite or delete the
historical design body.

- [ ] **Step 2: Add manual requests**

Add examples for:

1. a normal grounded question whose answer contains no source list or citation marker;
2. “Nội dung này nằm ở trang nào?” against a PDF chunk with page metadata;
3. the same question against DOCX/TXT or a legacy chunk without page metadata;
4. chat history after the schema migration;
5. hybrid search showing page/chunk fields but no highlight field.

- [ ] **Step 3: Audit active code**

Run:

```powershell
rg -n -i "citation|citations|ChatCitation|RagCitation|CitationInfo|IsHighlightable|AUTHORITATIVE_CITATION_PAGE|PAGE_CITATION_AVAILABLE" AIStudyHub.API AIStudyHub.Business AIStudyHub.Data --glob "!AIStudyHub.Data/Migrations/**"
```

Expected: zero matches in active application code.

Historical migrations are explicitly exempt because existing migrations must remain immutable. Approved/superseded design and plan documents are also historical records and must not be rewritten merely to make this search globally empty.

- [ ] **Step 4: Run final repository verification**

```powershell
dotnet build AIStudyHub.slnx --no-restore
git diff --check
git status --short
```

Expected: build succeeds, no whitespace error, no test project/file exists, and only intended source/documentation/new-migration changes remain.

- [ ] **Step 5: Commit documentation**

```powershell
git add docs/api-contract.md docs/backend-feature-status.md README.md AGENT.md ARCHITECTURE.md docs/FRONTEND_GUIDE.md docs/superpowers/specs/2026-07-16-persistent-chat-citations-design.md AIStudyHub.API/AIStudyHub.API.http
git diff --cached --check
git commit -m "docs: describe page-aware chat without citations"
```

---

## Manual Acceptance Checklist

The repository owner performs these checks after explicitly applying the new migration in their own test database:

- A normal grounded chat answer has no `citations` property, bracket marker, source list, or highlight metadata.
- Chat history has the same reduced response shape as a newly created message.
- Existing chat text remains readable after the citation table is removed.
- Retrieval still rejects results whose Document ID is invalid or outside the requested allowlist.
- Duplicate chunks do not appear repeatedly in the LLM context.
- RAG relevance, faithfulness, confidence, and token accounting still operate.
- “Nội dung này nằm ở trang nào?” returns the exact positive PDF page when that metadata exists.
- The same question says the exact page is unavailable when metadata is missing.
- A DOCX/TXT chunk never receives a fabricated page from `chunkIndex`.
- Normal answers do not mention a page unless the user explicitly asks.
- Hybrid search retains `PageNumber` and `ChunkIndex`, but returns no `IsHighlightable`.
- Applying the new migration removes only `ChatMessageCitation`; `ChatMessage` and `ChatSession` rows remain.
