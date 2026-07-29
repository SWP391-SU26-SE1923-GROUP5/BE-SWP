# Remove Citations While Keeping RAG Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove citation markers, API fields, orchestration types, persistence, and active documentation while retaining grounded RAG chat.

**Architecture:** Retrieval still supplies ranked document text to a source-free prompt context. Chat persists assistant content and relevance only; a new migration drops citation persistence after historical migrations have run.

**Tech Stack:** ASP.NET Core 8, Semantic Kernel orchestration, EF Core 8, AutoMapper, xUnit

## Global Constraints

- Keep hybrid retrieval, reranking, confidence, chat, and message history.
- Do not emit `[1]`, `[2]`, page references, or citation metadata.
- Keep existing citation migration files unchanged.
- Add a new migration that drops `ChatMessageCitation`.
- Active documentation must not advertise citation fields.

---

### Task 1: Simplify RAG result contracts and prompt context

**Files:**
- Modify: `AIStudyHub.Business/Interfaces/AI/Orchestration/OrchestrationTypes.cs`
- Modify: `AIStudyHub.Business/AI/Orchestration/RagPromptContextBuilder.cs`
- Modify: `AIStudyHub.Business/AI/Orchestration/SemanticKernelOrchestrator.cs`
- Modify: `AIStudyHub.Business/Services/BusinessServiceExtensions.cs`
- Delete: `AIStudyHub.Business/AI/Orchestration/RagCitationFactory.cs`
- Delete: `AIStudyHub.Business/AI/Orchestration/CitationHighlightability.cs`
- Create: `AIStudyHub.Tests/Services/RagWithoutCitationsTests.cs`

**Interfaces:**
- Produces: `RagResponse(string Answer, double Confidence, bool IsRelevant)`
- Produces: `RagResponseWithUsage(string Answer, double Confidence, int InputTokens, int OutputTokens, bool IsRelevant)`
- Produces: `RagPromptContextBuilder.Build(IEnumerable<SearchResult> results)`

- [ ] **Step 1: Write failing contract and prompt tests**

```csharp
[Fact]
public void RagResponse_HasNoCitationProperty()
{
    Assert.DoesNotContain(typeof(RagResponse).GetProperties(),
        property => property.Name.Contains("Citation", StringComparison.OrdinalIgnoreCase));
}

[Fact]
public void PromptContext_HasNoCitationMarkersOrPageInstructions()
{
    var result = new SearchResult(
        "content",
        1.0,
        "document.txt",
        new Dictionary<string, string>());
    var context = RagPromptContextBuilder.Build(new[] { result });
    Assert.DoesNotContain("CITATION", context, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("[1]", context);
    Assert.Contains("content", context);
}
```

- [ ] **Step 2: Run and confirm failure**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --no-restore --filter FullyQualifiedName~RagWithoutCitationsTests`

Expected: FAIL because RAG contracts and context include citations.

- [ ] **Step 3: Remove citation types and build plain ranked context**

Use:

```csharp
public record RagResponse(string Answer, double Confidence, bool IsRelevant);

public record RagResponseWithUsage(
    string Answer,
    double Confidence,
    int InputTokens,
    int OutputTokens,
    bool IsRelevant);
```

Build context from non-empty `SearchResult.Content`/text metadata in rank order with neutral separators. Remove citation-marker and authoritative-page prompt rules. Remove factory registration and constructor injection.

- [ ] **Step 4: Run RAG tests**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --no-restore --filter "FullyQualifiedName~RagWithoutCitationsTests|FullyQualifiedName~SemanticKernelStructuredExhaustiveTests"`

Expected: PASS after converting structured tests to the new contract.

- [ ] **Step 5: Commit**

```powershell
git add AIStudyHub.Business/Interfaces/AI/Orchestration/OrchestrationTypes.cs AIStudyHub.Business/AI/Orchestration/RagPromptContextBuilder.cs AIStudyHub.Business/AI/Orchestration/SemanticKernelOrchestrator.cs AIStudyHub.Business/Services/BusinessServiceExtensions.cs AIStudyHub.Business/AI/Orchestration/RagCitationFactory.cs AIStudyHub.Business/AI/Orchestration/CitationHighlightability.cs AIStudyHub.Tests/Services/RagWithoutCitationsTests.cs AIStudyHub.Tests/Services/SemanticKernelStructuredExhaustiveTests.cs
git commit -m "refactor: remove citations from rag orchestration"
```

### Task 2: Remove citations from Chat contracts and persistence logic

**Files:**
- Modify: `AIStudyHub.Business/DTOs/AIChat/ChatDtos.cs`
- Modify: `AIStudyHub.Business/AI/Chat/AIChatService.cs`
- Modify: `AIStudyHub.Business/Mappings/ApplicationMappingProfile.cs`
- Modify: `AIStudyHub.Data/Entities/ChatMessage.cs`
- Modify: `AIStudyHub.Data/ApplicationDbContext.cs`
- Modify: `AIStudyHub.Data/Configurations/EntityConfigurations.cs`
- Delete: `AIStudyHub.Data/Entities/ChatMessageCitation.cs`
- Create: `AIStudyHub.Tests/Services/ChatWithoutCitationsTests.cs`

**Interfaces:**
- Produces: `ChatMessageResponseDto` without a citations collection
- Produces: assistant persistence with `Content` and `IsRelevant`

- [ ] **Step 1: Write failing chat contract/history tests**

```csharp
[Fact]
public void ChatMessageResponse_HasNoCitationProperty()
{
    Assert.DoesNotContain(typeof(ChatMessageResponseDto).GetProperties(),
        property => property.Name.Contains("Citation", StringComparison.OrdinalIgnoreCase));
}

[Fact]
public async Task CreateThenReloadMessage_PreservesContentWithoutCitationRows()
{
    var created = await _service.CreateMessageAsync(_userId, _request, default);
    var history = await _service.GetMessagesAsync(created.ChatSessionId, _userId, default);
    Assert.Equal(created.Content, history.Last().Content);
}
```

- [ ] **Step 2: Run and confirm failure**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --no-restore --filter FullyQualifiedName~ChatWithoutCitationsTests`

Expected: FAIL because chat DTO/entity/persistence still expose citations.

- [ ] **Step 3: Remove citation mapping and entity usage**

Define the response as:

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

Remove `.Include(message => message.Citations)`, citation projection, citation entity construction, navigation, DbSet, and entity configuration. Persist the assistant ChatMessage with the RAG answer and relevance in the existing save.

- [ ] **Step 4: Run Chat tests**

Run: `dotnet test AIStudyHub.Tests/AIStudyHub.Tests.csproj --no-restore --filter "FullyQualifiedName~ChatWithoutCitationsTests|FullyQualifiedName~AIChatServiceTests"`

Expected: PASS after updating old assertions to the citation-free response.

- [ ] **Step 5: Commit**

```powershell
git add AIStudyHub.Business/DTOs/AIChat/ChatDtos.cs AIStudyHub.Business/AI/Chat/AIChatService.cs AIStudyHub.Business/Mappings/ApplicationMappingProfile.cs AIStudyHub.Data/Entities/ChatMessage.cs AIStudyHub.Data/ApplicationDbContext.cs AIStudyHub.Data/Configurations/EntityConfigurations.cs AIStudyHub.Data/Entities/ChatMessageCitation.cs AIStudyHub.Tests/Services/ChatWithoutCitationsTests.cs AIStudyHub.Tests/Services/AIChatServiceTests.cs
git commit -m "refactor: remove citations from chat"
```

### Task 3: Remove citation-only tests and update remaining contracts

**Files:**
- Delete: `AIStudyHub.Tests/Controllers/CitationControllerContractTests.cs`
- Delete: `AIStudyHub.Tests/Services/ChatCitationContractTests.cs`
- Delete: `AIStudyHub.Tests/Services/ChatMessageCitationConfigurationTests.cs`
- Delete: `AIStudyHub.Tests/Services/CitationHighlightabilityTests.cs`
- Delete: `AIStudyHub.Tests/Services/RagCitationFactoryTests.cs`
- Delete: `AIStudyHub.Tests/Services/RagPromptContextBuilderTests.cs`
- Delete: `AIStudyHub.Tests/Services/SemanticKernelCitationTests.cs`
- Modify: `AIStudyHub.Tests/Services/SemanticKernelStructuredExhaustiveTests.cs`
- Modify: `AIStudyHub.Business/DTOs/Rag/HybridSearchDtos.cs`

**Interfaces:**
- Consumes: citation-free RAG and Chat contracts from Tasks 1–2
- Produces: no active citation test dependency

- [ ] **Step 1: Search for active citation compile references**

Run:

```powershell
rg -n -i "citation" AIStudyHub.API AIStudyHub.Business AIStudyHub.Data AIStudyHub.Tests -g "!**/Migrations/**"
```

Expected: matches identify the exact remaining runtime/test references.

- [ ] **Step 2: Delete citation-only suites and convert mixed suites**

Remove tests whose only behavior is citation construction/persistence. In mixed Semantic Kernel tests, retain assertions for answer, relevance, confidence, cancellation, and usage. Remove citation-specific fields from hybrid response DTOs only where they are actually named or documented as citations; keep ordinary ranked search result metadata needed by search clients.

- [ ] **Step 3: Run all tests**

Run: `dotnet test AIStudyHub.slnx --no-restore`

Expected: PASS.

- [ ] **Step 4: Verify runtime citation references are gone**

Run:

```powershell
rg -n -i "citation" AIStudyHub.API AIStudyHub.Business AIStudyHub.Data AIStudyHub.Tests -g "!**/Migrations/**"
```

Expected: no matches.

- [ ] **Step 5: Commit**

```powershell
git add AIStudyHub.Tests AIStudyHub.Business/DTOs/Rag/HybridSearchDtos.cs
git commit -m "test: remove citation-only coverage"
```

### Task 4: Add the drop-table migration

**Files:**
- Create: `AIStudyHub.Data/Migrations/20260729120000_RemoveChatMessageCitations.cs`
- Create: `AIStudyHub.Data/Migrations/20260729120000_RemoveChatMessageCitations.Designer.cs`
- Modify: `AIStudyHub.Data/Migrations/ApplicationDbContextModelSnapshot.cs`

**Interfaces:**
- Produces: final EF model without `ChatMessageCitation`
- Preserves: `20260716061826_AddChatMessageCitations*`
- Preserves: `20260718140849_CompleteChatCitationFlow*`

- [ ] **Step 1: Generate migration**

Run: `dotnet ef migrations add RemoveChatMessageCitations --project AIStudyHub.Data/AIStudyHub.Data.csproj --startup-project AIStudyHub.API/AIStudyHub.API.csproj`

Expected: `Up` drops `ChatMessageCitation`; `Down` reconstructs its columns, constraints, index, and FK.

- [ ] **Step 2: Verify historical migrations are unchanged**

Run:

```powershell
git diff --exit-code -- AIStudyHub.Data/Migrations/20260716061826_AddChatMessageCitations.cs AIStudyHub.Data/Migrations/20260716061826_AddChatMessageCitations.Designer.cs AIStudyHub.Data/Migrations/20260718140849_CompleteChatCitationFlow.cs AIStudyHub.Data/Migrations/20260718140849_CompleteChatCitationFlow.Designer.cs
```

Expected: exit code 0.

- [ ] **Step 3: Run full tests**

Run: `dotnet test AIStudyHub.slnx --no-restore`

Expected: PASS.

- [ ] **Step 4: Commit**

```powershell
git add AIStudyHub.Data/Migrations
git commit -m "db: remove chat message citations"
```

### Task 5: Update active documentation and final verification

**Files:**
- Modify: `README.md`
- Modify: `ARCHITECTURE.md`
- Modify: `docs/FRONTEND_GUIDE.md`
- Modify: `docs/superpowers/specs/2026-07-16-persistent-chat-citations-design.md`

**Interfaces:**
- Produces: active docs describing citation-free Chat/RAG
- Marks: historical citation design as superseded by the 2026-07-29 design

- [ ] **Step 1: Mark the historical design as superseded**

Add directly below its title:

```markdown
> Superseded on 2026-07-29 by `2026-07-29-mentor-backend-remediation-design.md`.
> Citation persistence and response fields are no longer part of the active backend contract.
```

- [ ] **Step 2: Replace active API examples**

Remove `citations` arrays, citation-click behavior, marker rules, and citation sequence diagrams. Describe RAG responses using answer/content, confidence, relevance, and usage fields that remain in the final DTOs.

- [ ] **Step 3: Run repository citation audit**

Run:

```powershell
rg -n -i "citation" README.md ARCHITECTURE.md docs AIStudyHub.API AIStudyHub.Business AIStudyHub.Data AIStudyHub.Tests -g "!AIStudyHub.Data/Migrations/20260716061826_AddChatMessageCitations*" -g "!AIStudyHub.Data/Migrations/20260718140849_CompleteChatCitationFlow*" -g "!docs/superpowers/specs/2026-07-16-persistent-chat-citations-design.md" -g "!docs/superpowers/specs/2026-07-29-mentor-backend-remediation-design.md" -g "!docs/superpowers/plans/2026-07-29-remove-citations-keep-rag.md"
```

Expected: no matches.

- [ ] **Step 4: Run final build and tests**

Run: `dotnet test AIStudyHub.slnx --no-restore`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add README.md ARCHITECTURE.md docs/FRONTEND_GUIDE.md docs/superpowers/specs/2026-07-16-persistent-chat-citations-design.md
git commit -m "docs: document citation-free rag contract"
```
