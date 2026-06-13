# RAG Consolidation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Loại bỏ DocumentChunkingController và ChunkingFile, tích hợp vào DocumentUploadController + RagChatService

**Architecture:** Unified RAG architecture sử dụng DocumentUploadController làm entry point duy nhất. Chunking dùng DocumentProcessingService, retrieval dùng RagChatService.

**Tech Stack:** ASP.NET Core, MediatR, Entity Framework, Ollama, Pinecone (optional)

---

## File Structure

### Files sẽ xóa:
- `AIStudyHub.API/Controllers/DocumentChunkingController.cs`
- `AIStudyHub.Business/Features/DocumentChunks/` (toàn bộ folder)

### Files sẽ sửa:
- `AIStudyHub.Business/ultis/ChunkingFile.cs` - Xóa hoặc đánh dấu deprecated
- `AIStudyHub.API/Program.cs` - Xóa DI registration cho ChunkingFile

### Files sẽ thêm endpoints mới:
- `AIStudyHub.API/Controllers/DocumentUploadController.cs` - Thêm 3 endpoints

---

## Tasks

### Task 1: Khám phá codebase để hiểu cấu trúc hiện tại

**Files:**
- Read: `AIStudyHub.API/Controllers/DocumentChunkingController.cs`
- Read: `AIStudyHub.API/Controllers/DocumentUploadController.cs`
- Read: `AIStudyHub.Business/Services/RagChatService.cs`
- Read: `AIStudyHub.API/Program.cs`

---

### Task 2: Thêm endpoint GET /Documents/{id}/chunks vào DocumentUploadController

**Files:**
- Modify: `AIStudyHub.API/Controllers/DocumentUploadController.cs`

**Steps:**

- [ ] **Step 1: Thêm endpoint GetDocumentChunks**

Thêm vào `DocumentUploadController.cs` (sau các endpoint hiện có):

```csharp
[HttpGet("{id:guid}/chunks")]
public async Task<ActionResult<IEnumerable<ChunkDto>>> GetDocumentChunks(
    Guid id,
    CancellationToken cancellationToken)
{
    var document = await _unitOfWork.Documents.FindByIdAsync(id, cancellationToken);
    if (document == null)
        return NotFound("Document not found");

    var chunks = await _unitOfWork.DocumentChunks.FindAsync(
        c => c.DocumentId == id,
        cancellationToken: cancellationToken);

    var chunkDtos = chunks.Select(c => new ChunkDto(
        c.Id,
        c.DocumentId,
        c.ChunkJson ?? "",
        c.Index,
        c.CreatedAt
    )).OrderBy(c => c.Index).ToList();

    return Ok(chunkDtos);
}
```

- [ ] **Step 2: Commit**

```bash
git add AIStudyHub.API/Controllers/DocumentUploadController.cs
git commit -m "feat: add GET /Documents/{id}/chunks endpoint"
```

---

### Task 3: Thêm endpoint GET /Documents/{id}/chunks/search vào DocumentUploadController

**Files:**
- Modify: `AIStudyHub.API/Controllers/DocumentUploadController.cs`

**Steps:**

- [ ] **Step 1: Thêm endpoint SearchDocumentChunks**

Thêm vào `DocumentUploadController.cs`:

```csharp
[HttpGet("{id:guid}/chunks/search")]
public async Task<ActionResult<IEnumerable<ChunkDto>>> SearchDocumentChunks(
    Guid id,
    [FromQuery] string q,
    [FromQuery] int top = 5,
    CancellationToken cancellationToken = default)
{
    if (string.IsNullOrWhiteSpace(q))
        return BadRequest("Query parameter 'q' is required");

    var document = await _unitOfWork.Documents.FindByIdAsync(id, cancellationToken);
    if (document == null)
        return NotFound("Document not found");

    var chunks = await _unitOfWork.DocumentChunks.FindAsync(
        c => c.DocumentId == id,
        cancellationToken: cancellationToken);

    if (!chunks.Any())
        return Ok(Enumerable.Empty<ChunkDto>());

    var chunkTexts = chunks.Select(c => c.ChunkJson ?? "").ToList();
    var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(q);

    var scoredChunks = chunks.Select((c, i) => new
    {
        Chunk = c,
        Score = CosineSimilarity(queryEmbedding, GetEmbedding(chunkTexts[i]))
    })
    .Where(x => x.Score > 0.5f)
    .OrderByDescending(x => x.Score)
    .Take(top)
    .ToList();

    var result = scoredChunks.Select(x => new ChunkDto(
        x.Chunk.Id,
        x.Chunk.DocumentId,
        x.Chunk.ChunkJson ?? "",
        x.Chunk.Index,
        x.Chunk.CreatedAt
    ));

    return Ok(result);
}

private static float[] GetEmbedding(string text)
{
    // Simple hash-based embedding để match với GenerateSimpleEmbedding
    var dimension = 768;
    var embedding = new float[dimension];
    var words = text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

    foreach (var word in words)
    {
        var hash = word.GetHashCode();
        for (var i = 0; i < dimension; i++)
        {
            embedding[i] += (float)Math.Sin(hash * (i + 1) * 0.1) * 0.01f;
        }
    }

    var magnitude = (float)Math.Sqrt(embedding.Sum(e => e * e));
    if (magnitude > 0)
    {
        for (var i = 0; i < dimension; i++)
            embedding[i] /= magnitude;
    }

    return embedding;
}

private static float CosineSimilarity(float[] a, float[] b)
{
    if (a.Length != b.Length)
        return 0;

    var dotProduct = a.Zip(b, (x, y) => x * y).Sum();
    var magnitudeA = (float)Math.Sqrt(a.Sum(x => x * x));
    var magnitudeB = (float)Math.Sqrt(b.Sum(x => x * x));

    return magnitudeA > 0 && magnitudeB > 0 ? dotProduct / (magnitudeA * magnitudeB) : 0;
}
```

- [ ] **Step 2: Commit**

```bash
git add AIStudyHub.API/Controllers/DocumentUploadController.cs
git commit -m "feat: add GET /Documents/{id}/chunks/search endpoint"
```

---

### Task 4: Thêm endpoint POST /Documents/{id}/chat vào DocumentUploadController

**Files:**
- Modify: `AIStudyHub.API/Controllers/DocumentUploadController.cs`

**Steps:**

- [ ] **Step 1: Thêm ChatWithDocument request/handler nếu chưa có**

Kiểm tra xem đã có request này trong Features/Chat chưa. Nếu chưa, tạo:

**Create:** `AIStudyHub.Business/Features/Chat/ChatDocumentRequests.cs`

```csharp
namespace AIStudyHub.Business.Features.Chat;

public record ChatDocumentRequest(
    Guid DocumentId,
    string Message,
    int MaxChunks = 5
) : IRequest<ChatResponse>;
```

- [ ] **Step 2: Thêm ChatDocumentHandler vào Features/Chat**

**Create:** `AIStudyHub.Business/Features/Chat/ChatDocumentHandler.cs`

```csharp
using AIStudyHub.Business.Interfaces.Services;

namespace AIStudyHub.Business.Features.Chat;

public class ChatDocumentHandler : IRequestHandler<ChatDocumentRequest, ChatResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmbeddingService _embeddingService;
    private readonly IRagChatService _ragChatService;

    public ChatDocumentHandler(
        IUnitOfWork unitOfWork,
        IEmbeddingService embeddingService,
        IRagChatService ragChatService)
    {
        _unitOfWork = unitOfWork;
        _embeddingService = embeddingService;
        _ragChatService = ragChatService;
    }

    public async Task<ChatResponse> Handle(ChatDocumentRequest request, CancellationToken cancellationToken)
    {
        var document = await _unitOfWork.Documents.FindByIdAsync(request.DocumentId, cancellationToken);
        if (document == null)
            throw new InvalidOperationException("Document not found");

        var chunks = await _unitOfWork.DocumentChunks.FindAsync(
            c => c.DocumentId == request.DocumentId,
            cancellationToken: cancellationToken);

        if (!chunks.Any())
            throw new InvalidOperationException("Document has no chunks. Please process the document first.");

        var relevantChunks = await _ragChatService.GetRelevantChunksAsync(
            request.Message,
            chunks.ToList(),
            request.MaxChunks,
            cancellationToken);

        var context = _ragChatService.BuildContext(relevantChunks, document.Name);

        var answer = await _ragChatService.GenerateAnswerAsync(
            request.Message,
            context,
            cancellationToken);

        var citations = _ragChatService.ExtractCitations(answer, relevantChunks);

        return new ChatResponse(answer, citations, relevantChunks.Count);
    }
}
```

- [ ] **Step 3: Register handler trong DI**

Thêm vào `AIStudyHub.Business/DependencyInjection.cs`:

```csharp
services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ChatDocumentHandler).Assembly));
```

- [ ] **Step 4: Thêm endpoint vào DocumentUploadController**

Thêm vào `DocumentUploadController.cs`:

```csharp
[HttpPost("{id:guid}/chat")]
public async Task<ActionResult<ChatResponse>> ChatWithDocument(
    Guid id,
    [FromBody] ChatRequest request,
    CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(request?.Message))
        return BadRequest("Message is required");

    try
    {
        var response = await _mediator.Send(
            new ChatDocumentRequest(id, request.Message, request.MaxChunks ?? 5),
            cancellationToken);

        return Ok(response);
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
    {
        return NotFound(ex.Message);
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("no chunks"))
    {
        return BadRequest(ex.Message);
    }
}
```

- [ ] **Step 5: Commit**

```bash
git add AIStudyHub.Business/Features/Chat/ChatDocumentRequests.cs
git add AIStudyHub.Business/Features/Chat/ChatDocumentHandler.cs
git add AIStudyHub.Business/DependencyInjection.cs
git add AIStudyHub.API/Controllers/DocumentUploadController.cs
git commit -m "feat: add POST /Documents/{id}/chat endpoint"
```

---

### Task 5: Xóa DocumentChunkingController

**Files:**
- Delete: `AIStudyHub.API/Controllers/DocumentChunkingController.cs`

**Steps:**

- [ ] **Step 1: Xóa file**

```bash
rm AIStudyHub.API/Controllers/DocumentChunkingController.cs
```

- [ ] **Step 2: Commit**

```bash
git add -A
git commit -m "refactor: remove DocumentChunkingController"
```

---

### Task 6: Xóa DocumentChunks MediatR folder

**Files:**
- Delete: `AIStudyHub.Business/Features/DocumentChunks/` (toàn bộ folder)

**Steps:**

- [ ] **Step 1: Xóa folder**

```bash
rm -rf AIStudyHub.Business/Features/DocumentChunks/
```

- [ ] **Step 2: Commit**

```bash
git add -A
git commit -m "refactor: remove DocumentChunks MediatR folder"
```

---

### Task 7: Xóa ChunkingFile và cleanup DI

**Files:**
- Delete: `AIStudyHub.Business/ultis/ChunkingFile.cs`
- Modify: `AIStudyHub.API/Program.cs`

**Steps:**

- [ ] **Step 1: Kiểm tra ChunkingFile có được dùng ở đâu khác không**

```bash
grep -r "ChunkingFile" AIStudyHub.Business/ AIStudyHub.API/
```

Nếu chỉ có ở DI registration, tiếp tục bước 2.

- [ ] **Step 2: Xóa ChunkingFile**

```bash
rm AIStudyHub.Business/ultis/ChunkingFile.cs
```

- [ ] **Step 3: Xóa DI registration trong Program.cs**

Tìm và xóa dòng tương tự:
```csharp
// Xóa: services.AddScoped<ChunkingFile>();
```

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "refactor: remove ChunkingFile utility"
```

---

### Task 8: Cleanup và verify

**Steps:**

- [ ] **Step 1: Build project để verify không có lỗi**

```bash
dotnet build AIStudyHub.API/AIStudyHub.API.csproj
```

Expected: Build succeeded

- [ ] **Step 2: Kiểm tra không còn reference đến ChunkingFile**

```bash
grep -r "ChunkingFile" AIStudyHub/
```

Expected: No results

- [ ] **Step 3: Commit final**

```bash
git add -A
git commit -m "chore: RAG consolidation complete"
```

---

## Verification

Sau khi hoàn thành, test flow:

1. **Upload document** → `POST /api/Documents/upload`
2. **Get chunks** → `GET /api/Documents/{id}/chunks`
3. **Search chunks** → `GET /api/Documents/{id}/chunks/search?q=keyword`
4. **Chat with document** → `POST /api/Documents/{id}/chat`

---

## Spec Coverage Checklist

- [x] Xóa DocumentChunkingController
- [x] Xóa ChunkingFile
- [x] Xóa DocumentChunks MediatR folder
- [x] Thêm endpoint GET /Documents/{id}/chunks
- [x] Thêm endpoint GET /Documents/{id}/chunks/search
- [x] Thêm endpoint POST /Documents/{id}/chat
- [x] Xóa ChunkingFile khỏi DI registration
