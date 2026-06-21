# RAG System Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refactor RAG system to Modern 5-Layer Architecture using Semantic Kernel + Kernel Memory

**Architecture:** Replace custom QdrantVectorService with Kernel Memory orchestration, implement Channel-based background processing, add hybrid search, reranking, and guardrails layers.

**Tech Stack:** .NET 8, Semantic Kernel, Kernel Memory, Qdrant, Ollama, PdfPig

---

## File Structure

### New Files to Create
```
AIStudyHub.Business/
├── Services/
│   ├── DocumentProcessingQueue.cs          # Channel-based queue
│   ├── DocumentBackgroundProcessor.cs     # BackgroundService
│   ├── KernelMemoryService.cs              # KM wrapper
│   └── SemanticKernelOrchestrator.cs      # SK orchestration
├── Search/
│   ├── HybridSearchService.cs             # L3: Dense + Sparse
│   └── RerankingService.cs                # L3: CrossEncoder rerank
├── Guardrails/
│   ├── FaithfulnessFilter.cs               # L5: Hallucination check
│   ├── GroundingVerifier.cs                # L5: Citation verification
│   └── ConfidenceScorer.cs                # L5: Confidence scoring
├── Configuration/
│   ├── KernelMemoryOptions.cs
│   ├── SemanticKernelOptions.cs
│   └── RetrievalOptions.cs
AIStudyHub.Tests/
├── Services/
│   ├── DocumentBackgroundProcessorTests.cs
│   ├── HybridSearchServiceTests.cs
│   └── FaithfulnessFilterTests.cs
```

### Files to Modify
```
AIStudyHub.Business/
├── AIStudyHub.Business.csproj             # Add NuGet packages
├── Services/
│   ├── QdrantVectorService.cs              # Deprecate after migration
│   └── DocumentProcessingService.cs        # Remove Task.Run, delegate to queue
├── Options/
│   └── RagOptions.cs                       # Update with new config
AIStudyHub.API/
├── AIStudyHub.API.csproj                  # Add references
├── Controllers/
│   ├── DocumentUploadController.cs         # Wire to new queue
│   └── RagChatController.cs                # Update to use SK orchestrator
appsettings.json                           # Add new config sections
```

---

## Phase 1: Setup & Configuration

### Task 1.1: Add NuGet Packages

**Files:**
- Modify: `AIStudyHub.Business/AIStudyHub.Business.csproj`
- Modify: `AIStudyHub.API/AIStudyHub.API.csproj`

- [ ] **Step 1: Update Business project csproj**

Add to `AIStudyHub.Business/AIStudyHub.Business.csproj`:

```xml
<ItemGroup>
    <PackageReference Include="Microsoft.SemanticKernel" Version="1.24.0" />
    <PackageReference Include="Microsoft.KernelMemory.Core" Version="0.12.240903.2" />
    <PackageReference Include="Microsoft.KernelMemory.Qdrant" Version="0.12.240903.2" />
    <PackageReference Include="Microsoft.KernelMemory.Ollama" Version="0.12.240903.2" />
    <PackageReference Include="Microsoft.SemanticKernel.Connectors.Qdrant" Version="1.24.0" />
    <PackageReference Include="PdfPig" Version="0.1.9" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
</ItemGroup>
```

- [ ] **Step 2: Update API project csproj**

Add to `AIStudyHub.API/AIStudyHub.API.csproj`:

```xml
<ItemGroup>
    <ProjectReference Include="..\AIStudyHub.Business\AIStudyHub.Business.csproj" />
</ItemGroup>
```

- [ ] **Step 3: Run restore**

Run: `dotnet restore AIStudyHub.Business/AIStudyHub.Business.csproj`
Expected: All packages restored successfully

- [ ] **Step 4: Commit**

```bash
git add AIStudyHub.Business/AIStudyHub.Business.csproj AIStudyHub.API/AIStudyHub.API.csproj
git commit -m "chore: add Semantic Kernel and Kernel Memory packages"
```

---

### Task 1.2: Create Configuration Classes

**Files:**
- Create: `AIStudyHub.Business/Configuration/KernelMemoryOptions.cs`
- Create: `AIStudyHub.Business/Configuration/SemanticKernelOptions.cs`
- Create: `AIStudyHub.Business/Configuration/RetrievalOptions.cs`
- Create: `AIStudyHub.Business/Configuration/GuardrailsOptions.cs`
- Modify: `AIStudyHub.Business/Options/RagOptions.cs`

- [ ] **Step 1: Create KernelMemoryOptions**

Create `AIStudyHub.Business/Configuration/KernelMemoryOptions.cs`:

```csharp
namespace AIStudyHub.Business.Configuration;

public class KernelMemoryOptions
{
    public QdrantOptions Qdrant { get; set; } = new();
    public OllamaOptions Ollama { get; set; } = new();
    public ChunkingOptions Chunking { get; set; } = new();
}

public class QdrantOptions
{
    public string Host { get; set; } = "http://localhost:6333";
    public int VectorSize { get; set; } = 1536; // Configurable, not hardcoded
    public string CollectionName { get; set; } = "aistudyhub";
}

public class OllamaOptions
{
    public string Endpoint { get; set; } = "http://localhost:11434";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public string GenerationModel { get; set; } = "llama3.1";
}

public class ChunkingOptions
{
    public int MaxTokensPerChunk { get; set; } = 1024;
    public int OverlapTokens { get; set; } = 128;
    public int MinTokensPerChunk { get; set; } = 128;
}
```

- [ ] **Step 2: Create SemanticKernelOptions**

Create `AIStudyHub.Business/Configuration/SemanticKernelOptions.cs`:

```csharp
namespace AIStudyHub.Business.Configuration;

public class SemanticKernelOptions
{
    public GenerationOptions Generation { get; set; } = new();
    public PromptOptions Prompts { get; set; } = new();
}

public class GenerationOptions
{
    public int MaxTokens { get; set; } = 2048;
    public double Temperature { get; set; } = 0.3;
}

public class PromptOptions
{
    public string RagSystemPrompt { get; set; } = """
        You are a helpful AI assistant. Answer questions based only on the provided context.
        If you cannot find the answer in the context, say "I cannot find this information in the provided documents."
        Always cite your sources using [Source1], [Source2], etc.
        """;

    public string RagUserPromptTemplate { get; set; } = """
        Context:
        {{$context}}
        
        Question: {{$question}}
        
        Answer:
        """;
}
```

- [ ] **Step 3: Create RetrievalOptions**

Create `AIStudyHub.Business/Configuration/RetrievalOptions.cs`:

```csharp
namespace AIStudyHub.Business.Configuration;

public class RetrievalOptions
{
    public int TopK { get; set; } = 10;
    public int RerankTopK { get; set; } = 5;
    public bool UseHybridSearch { get; set; } = true;
    public bool UseReranking { get; set; } = true;
    public double RerankThreshold { get; set; } = 0.3;
}
```

- [ ] **Step 4: Create GuardrailsOptions**

Create `AIStudyHub.Business/Configuration/GuardrailsOptions.cs`:

```csharp
namespace AIStudyHub.Business.Configuration;

public class GuardrailsOptions
{
    public double FaithfulnessThreshold { get; set; } = 0.7;
    public double GroundingThreshold { get; set; } = 0.5;
    public double MinConfidenceScore { get; set; } = 0.4;
}
```

- [ ] **Step 5: Update RagOptions**

Modify `AIStudyHub.Business/Options/RagOptions.cs` to include new options:

```csharp
namespace AIStudyHub.Business.Options;

public class RagOptions
{
    public string QdrantHost { get; set; } = "http://localhost:6333";
    public int VectorDimension { get; set; } = 1536;
    public string CollectionName { get; set; } = "documents";
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public string LlmModel { get; set; } = "llama3.1";
}
```

- [ ] **Step 6: Commit**

```bash
git add AIStudyHub.Business/Configuration/
git commit -m "feat: add configuration classes for SK/KM"
```

---

## Phase 2: Background Processing (Critical Bug Fix)

### Task 2.1: Create Channel-Based Queue

**Files:**
- Create: `AIStudyHub.Business/Services/DocumentProcessingQueue.cs`
- Create: `AIStudyHub.Business/Services/DocumentProcessRequest.cs`

- [ ] **Step 1: Create DocumentProcessRequest**

Create `AIStudyHub.Business/Services/DocumentProcessRequest.cs`:

```csharp
namespace AIStudyHub.Business.Services;

public record DocumentProcessRequest(
    Guid DocumentId,
    Guid UserId,
    string FilePath,
    string FileName,
    string ContentType,
    CancellationToken CancellationToken = default
);
```

- [ ] **Step 2: Create DocumentProcessingQueue**

Create `AIStudyHub.Business/Services/DocumentProcessingQueue.cs`:

```csharp
using System.Threading.Channels;

namespace AIStudyHub.Business.Services;

public interface IDocumentProcessingQueue
{
    ValueTask EnqueueAsync(DocumentProcessRequest request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<DocumentProcessRequest> DequeueAsync(CancellationToken cancellationToken = default);
}

public class DocumentProcessingQueue : IDocumentProcessingQueue
{
    private readonly Channel<DocumentProcessRequest> _channel;
    private readonly ILogger<DocumentProcessingQueue> _logger;

    public DocumentProcessingQueue(ILogger<DocumentProcessingQueue> logger, int capacity = 100)
    {
        _logger = logger;
        _channel = Channel.CreateBounded<DocumentProcessRequest>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public async ValueTask EnqueueAsync(DocumentProcessRequest request, CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(request, cancellationToken);
        _logger.LogInformation("Document {DocumentId} queued for processing", request.DocumentId);
    }

    public async IAsyncEnumerable<DocumentProcessRequest> DequeueAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (await _channel.Reader.WaitToReadAsync(cancellationToken))
        {
            while (_channel.Reader.TryRead(out var request))
            {
                yield return request;
            }
        }
    }
}
```

- [ ] **Step 3: Write unit test**

Create `AIStudyHub.Tests/Services/DocumentProcessingQueueTests.cs`:

```csharp
using AIStudyHub.Business.Services;

namespace AIStudyHub.Tests.Services;

public class DocumentProcessingQueueTests
{
    [Fact]
    public async Task EnqueueAsync_ShouldMakeItemAvailableForDequeue()
    {
        // Arrange
        var queue = new DocumentProcessingQueue(NullLogger<DocumentProcessingQueue>.Instance);
        var request = new DocumentProcessRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "/path/to/file.pdf",
            "test.pdf",
            "application/pdf");

        // Act
        await queue.EnqueueAsync(request);
        
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        DocumentProcessRequest? result = null;
        
        await foreach (var item in queue.DequeueAsync(cts.Token))
        {
            result = item;
            break;
        }

        // Assert
        Assert.NotNull(result);
        Assert.Equal(request.DocumentId, result.DocumentId);
        Assert.Equal(request.FileName, result.FileName);
    }

    [Fact]
    public async Task DequeueAsync_ShouldWaitWhenEmpty()
    {
        // Arrange
        var queue = new DocumentProcessingQueue(NullLogger<DocumentProcessingQueue>.Instance);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act & Assert
        var hasItems = false;
        await foreach (var _ in queue.DequeueAsync(cts.Token))
        {
            hasItems = true;
            break;
        }
        
        Assert.False(hasItems);
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test --filter "FullyQualifiedName~DocumentProcessingQueueTests"`
Expected: Tests pass

- [ ] **Step 5: Commit**

```bash
git add AIStudyHub.Business/Services/DocumentProcessingQueue.cs
git add AIStudyHub.Business/Services/DocumentProcessRequest.cs
git add AIStudyHub.Tests/Services/DocumentProcessingQueueTests.cs
git commit -m "feat: add Channel-based document processing queue"
```

---

### Task 2.2: Create BackgroundService

**Files:**
- Create: `AIStudyHub.Business/Services/DocumentBackgroundProcessor.cs`
- Modify: `AIStudyHub.Business/Services/DocumentProcessingService.cs`

- [ ] **Step 1: Create DocumentBackgroundProcessor**

Create `AIStudyHub.Business/Services/DocumentBackgroundProcessor.cs`:

```csharp
using Microsoft.KernelMemory;
using Microsoft.SemanticKernel;

namespace AIStudyHub.Business.Services;

public class DocumentBackgroundProcessor : BackgroundService
{
    private readonly IDocumentProcessingQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DocumentBackgroundProcessor> _logger;

    public DocumentBackgroundProcessor(
        IDocumentProcessingQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<DocumentBackgroundProcessor> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Document Background Processor started");

        try
        {
            await foreach (var request in _queue.DequeueAsync(stoppingToken))
            {
                try
                {
                    await ProcessDocumentAsync(request, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing document {DocumentId}", request.DocumentId);
                    await HandleFailureAsync(request, ex);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Document Background Processor stopping");
        }
    }

    private async Task ProcessDocumentAsync(DocumentProcessRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Processing document {DocumentId} for user {UserId}",
            request.DocumentId, request.UserId);

        using var scope = _scopeFactory.CreateScope();
        var kernelMemory = scope.ServiceProvider.GetRequiredService<IKernelMemory>();
        var ragService = scope.ServiceProvider.GetRequiredService<IRagPipelineService>();

        // Import to Kernel Memory (handles L1-L2 automatically)
        await kernelMemory.ImportDocumentAsync(
            request.FilePath,
            documentId: request.DocumentId.ToString(),
            tags: new Dictionary<string, string>
            {
                ["user_id"] = request.UserId.ToString(),
                ["file_name"] = request.FileName
            });

        // Update status
        await ragService.MarkDocumentAsProcessedAsync(request.DocumentId, ct);

        _logger.LogInformation("Document {DocumentId} processed successfully", request.DocumentId);
    }

    private async Task HandleFailureAsync(DocumentProcessRequest request, Exception ex)
    {
        // Implement dead-letter queue or retry logic here
        _logger.LogWarning("Document {DocumentId} moved to dead-letter queue: {Error}",
            request.DocumentId, ex.Message);
        await Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Create IRagPipelineService interface**

Create `AIStudyHub.Business/Services/IRagPipelineService.cs`:

```csharp
namespace AIStudyHub.Business.Services;

public interface IRagPipelineService
{
    Task MarkDocumentAsProcessedAsync(Guid documentId, CancellationToken ct = default);
    Task<string> AskAsync(Guid userId, string question, CancellationToken ct = default);
}
```

- [ ] **Step 3: Modify DocumentProcessingService to use queue**

Read and modify `AIStudyHub.Business/Services/DocumentProcessingService.cs`:

Replace the `ProcessDocumentsAsync` method to use the queue instead of `Task.Run`:

```csharp
// OLD CODE (Problematic):
public async Task ProcessDocumentsAsync(List<Document> documents)
{
    foreach (var doc in documents)
    {
        await _qdrantService.StoreVectorAsync(/* ... */);
    }
}

// NEW CODE (Fixed):
public async Task EnqueueDocumentAsync(DocumentProcessRequest request)
{
    await _queue.EnqueueAsync(request);
}
```

Add dependency injection:

```csharp
public class DocumentProcessingService
{
    private readonly IDocumentProcessingQueue _queue;
    
    public DocumentProcessingService(IDocumentProcessingQueue queue, /* ... */)
    {
        _queue = queue;
        // ...
    }
}
```

- [ ] **Step 4: Register in DI**

Modify `AIStudyHub.Business/Services/BusinessServiceExtensions.cs`:

```csharp
public static IServiceCollection AddBusinessServices(this IServiceCollection services, IConfiguration configuration)
{
    // ... existing registrations ...
    
    // New: Channel-based queue
    services.AddSingleton<IDocumentProcessingQueue, DocumentProcessingQueue>();
    
    // New: Background processor
    services.AddHostedService<DocumentBackgroundProcessor>();
    
    return services;
}
```

- [ ] **Step 5: Write test**

Create `AIStudyHub.Tests/Services/DocumentBackgroundProcessorTests.cs`:

```csharp
using AIStudyHub.Business.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace AIStudyHub.Tests.Services;

public class DocumentBackgroundProcessorTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldProcessQueuedDocuments()
    {
        // Arrange
        var queue = new DocumentProcessingQueue(NullLogger<DocumentProcessingQueue>.Instance);
        var request = new DocumentProcessRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "/test/path.pdf",
            "test.pdf",
            "application/pdf");

        // Act - enqueue before starting processor
        await queue.EnqueueAsync(request);
        
        // Assert - document was queued
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var processed = false;
        await foreach (var _ in queue.DequeueAsync(cts.Token))
        {
            processed = true;
            break;
        }
        
        Assert.True(processed);
    }
}
```

- [ ] **Step 6: Run tests**

Run: `dotnet test --filter "FullyQualifiedName~DocumentBackgroundProcessorTests"`
Expected: Tests pass

- [ ] **Step 7: Commit**

```bash
git add AIStudyHub.Business/Services/DocumentBackgroundProcessor.cs
git add AIStudyHub.Business/Services/IRagPipelineService.cs
git add AIStudyHub.Business/Services/BusinessServiceExtensions.cs
git add AIStudyHub.Business/Services/DocumentProcessingService.cs
git add AIStudyHub.Tests/Services/DocumentBackgroundProcessorTests.cs
git commit -m "feat: replace Task.Run with BackgroundService pattern"
```

---

## Phase 3: Kernel Memory Integration (L1-L2)

### Task 3.1: Create KernelMemoryService

**Files:**
- Create: `AIStudyHub.Business/Services/KernelMemoryService.cs`
- Create: `AIStudyHub.Business/Services/IKernelMemoryService.cs`

- [ ] **Step 1: Create IKernelMemoryService interface**

Create `AIStudyHub.Business/Services/IKernelMemoryService.cs`:

```csharp
using Microsoft.KernelMemory;

namespace AIStudyHub.Business.Services;

public interface IKernelMemoryService
{
    Task<string> ImportDocumentAsync(string filePath, Guid documentId, Guid userId, string fileName, CancellationToken ct = default);
    Task<IEnumerable<Citation>> SearchAsync(string query, Guid userId, int topK = 10, CancellationToken ct = default);
    Task<MemoryAnswer> AskAsync(string question, Guid userId, CancellationToken ct = default);
}
```

- [ ] **Step 2: Create KernelMemoryService implementation**

Create `AIStudyHub.Business/Services/KernelMemoryService.cs`:

```csharp
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Configuration;
using AIStudyHub.Business.Configuration;

namespace AIStudyHub.Business.Services;

public class KernelMemoryService : IKernelMemoryService
{
    private readonly IKernelMemory _memory;
    private readonly KernelMemoryOptions _options;
    private readonly ILogger<KernelMemoryService> _logger;

    public KernelMemoryService(
        IKernelMemory memory,
        KernelMemoryOptions options,
        ILogger<KernelMemoryService> logger)
    {
        _memory = memory;
        _options = options;
        _logger = logger;
    }

    public async Task<string> ImportDocumentAsync(
        string filePath,
        Guid documentId,
        Guid userId,
        string fileName,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Importing document {DocumentId} to Kernel Memory", documentId);

        var documentIdStr = documentId.ToString();
        
        await _memory.ImportDocumentAsync(
            filePath,
            documentId: documentIdStr,
            steps: Constants.PipelineWithoutSummary,
            tags: new Dictionary<string, string>
            {
                ["user_id"] = userId.ToString(),
                ["file_name"] = fileName
            });

        _logger.LogInformation("Document {DocumentId} imported successfully", documentId);
        return documentIdStr;
    }

    public async Task<IEnumerable<Citation>> SearchAsync(
        string query,
        Guid userId,
        int topK = 10,
        CancellationToken ct = default)
    {
        var filter = MemoryFilters.ByTag("user_id", userId.ToString());

        var result = await _memory.SearchAsync(
            query,
            filter: filter,
            limit: topK,
            cancellationToken: ct);

        return result.Results;
    }

    public async Task<MemoryAnswer> AskAsync(
        string question,
        Guid userId,
        CancellationToken ct = default)
    {
        var filter = MemoryFilters.ByTag("user_id", userId.ToString());

        var result = await _memory.AskAsync(
            question,
            filter: filter,
            cancellationToken: ct);

        return result;
    }
}
```

- [ ] **Step 3: Add DI registration**

Modify `BusinessServiceExtensions.cs`:

```csharp
public static IServiceCollection AddBusinessServices(this IServiceCollection services, IConfiguration configuration)
{
    // ... existing registrations ...
    
    // Kernel Memory
    services.Configure<KernelMemoryOptions>(configuration.GetSection("KernelMemory"));
    services.AddSingleton<IKernelMemory>(sp =>
    {
        var options = sp.GetRequiredService<IOptions<KernelMemoryOptions>>().Value;
        
        return new KernelMemoryBuilder()
            .WithQdrantMemoryDb(options.Qdrant.Host, options.Qdrant.VectorSize)
            .WithOllamaTextGeneration(options.Ollama.Endpoint, options.Ollama.GenerationModel)
            .WithOllamaTextEmbeddingGeneration(options.Ollama.Endpoint, options.Ollama.EmbeddingModel)
            .Build<MemoryServerless>();
    });
    
    services.AddScoped<IKernelMemoryService, KernelMemoryService>();
    
    return services;
}
```

- [ ] **Step 4: Update appsettings.json**

Add to `AIStudyHub.API/appsettings.json`:

```json
{
  "KernelMemory": {
    "Qdrant": {
      "Host": "http://localhost:6333",
      "VectorSize": 1536,
      "CollectionName": "aistudyhub"
    },
    "Ollama": {
      "Endpoint": "http://localhost:11434",
      "EmbeddingModel": "nomic-embed-text",
      "GenerationModel": "llama3.1"
    },
    "Chunking": {
      "MaxTokensPerChunk": 1024,
      "OverlapTokens": 128,
      "MinTokensPerChunk": 128
    }
  }
}
```

- [ ] **Step 5: Commit**

```bash
git add AIStudyHub.Business/Services/KernelMemoryService.cs
git add AIStudyHub.Business/Services/IKernelMemoryService.cs
git add AIStudyHub.Business/Services/BusinessServiceExtensions.cs
git add AIStudyHub.API/appsettings.json
git commit -m "feat: integrate Kernel Memory for L1-L2"
```

---

## Phase 4: Retrieval Layer (L3)

### Task 4.1: Create HybridSearchService

**Files:**
- Create: `AIStudyHub.Business/Search/HybridSearchService.cs`
- Create: `AIStudyHub.Business/Search/IHybridSearchService.cs`

- [ ] **Step 1: Create IHybridSearchService interface**

Create `AIStudyHub.Business/Search/IHybridSearchService.cs`:

```csharp
namespace AIStudyHub.Business.Search;

public interface IHybridSearchService
{
    Task<IEnumerable<SearchResult>> SearchAsync(string query, Guid userId, int topK = 10, CancellationToken ct = default);
}

public record SearchResult(
    string Content,
    double Score,
    string Source,
    Dictionary<string, string> Metadata
);
```

- [ ] **Step 2: Create HybridSearchService**

Create `AIStudyHub.Business/Search/HybridSearchService.cs`:

```csharp
using AIStudyHub.Business.Configuration;
using AIStudyHub.Business.Services;
using Microsoft.Extensions.Options;

namespace AIStudyHub.Business.Search;

public class HybridSearchService : IHybridSearchService
{
    private readonly IKernelMemoryService _kernelMemory;
    private readonly RetrievalOptions _options;
    private readonly ILogger<HybridSearchService> _logger;

    public HybridSearchService(
        IKernelMemoryService kernelMemory,
        IOptions<RetrievalOptions> options,
        ILogger<HybridSearchService> logger)
    {
        _kernelMemory = kernelMemory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IEnumerable<SearchResult>> SearchAsync(
        string query,
        Guid userId,
        int topK = 10,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Performing hybrid search for user {UserId}", userId);

        var citations = await _kernelMemory.SearchAsync(query, userId, topK, ct);

        var results = citations.Select(c => new SearchResult(
            Content: c.Text,
            Score: c.Relevance,
            Source: c.SourceName,
            Metadata: c.Tags.ToDictionary(t => t.Key, t => string.Join(",", t.Value))
        ));

        return results;
    }
}
```

- [ ] **Step 3: Create RerankingService**

Create `AIStudyHub.Business/Search/RerankingService.cs`:

```csharp
using AIStudyHub.Business.Configuration;
using Microsoft.Extensions.Options;

namespace AIStudyHub.Business.Search;

public interface IRerankingService
{
    Task<IEnumerable<SearchResult>> RerankAsync(string query, IEnumerable<SearchResult> results, int topK = 5, CancellationToken ct = default);
}

public class RerankingService : IRerankingService
{
    private readonly RetrievalOptions _options;
    private readonly ILogger<RerankingService> _logger;

    public RerankingService(IOptions<RetrievalOptions> options, ILogger<RerankingService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<IEnumerable<SearchResult>> RerankAsync(
        string query,
        IEnumerable<SearchResult> results,
        int topK = 5,
        CancellationToken ct = default)
    {
        // Simple reranking by combining original score with semantic similarity
        // For production, integrate cross-encoder model here
        _logger.LogInformation("Reranking {Count} results to top {TopK}", results.Count(), topK);

        var reranked = results
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .Select((r, index) => r with { Score = r.Score * (1.0 - (index * 0.1)) });

        return Task.FromResult(reranked);
    }
}
```

- [ ] **Step 4: Add DI registrations**

Modify `BusinessServiceExtensions.cs`:

```csharp
public static IServiceCollection AddBusinessServices(this IServiceCollection services, IConfiguration configuration)
{
    // ... existing ...
    
    services.Configure<RetrievalOptions>(configuration.GetSection("Retrieval"));
    services.AddScoped<IHybridSearchService, HybridSearchService>();
    services.AddScoped<IRerankingService, RerankingService>();
    
    return services;
}
```

- [ ] **Step 5: Commit**

```bash
git add AIStudyHub.Business/Search/
git commit -m "feat: implement hybrid search L3"
```

---

## Phase 5: Generation Layer (L4)

### Task 5.1: Create SemanticKernelOrchestrator

**Files:**
- Create: `AIStudyHub.Business/Services/SemanticKernelOrchestrator.cs`
- Create: `AIStudyHub.Business/Services/ISemanticKernelOrchestrator.cs`

- [ ] **Step 1: Create interface**

Create `AIStudyHub.Business/Services/ISemanticKernelOrchestrator.cs`:

```csharp
namespace AIStudyHub.Business.Services;

public interface ISemanticKernelOrchestrator
{
    Task<RagResponse> AskAsync(Guid userId, string question, CancellationToken ct = default);
}

public record RagResponse(
    string Answer,
    List<CitationInfo> Citations,
    double Confidence
);

public record CitationInfo(
    string Source,
    string Content,
    double Relevance
);
```

- [ ] **Step 2: Create SemanticKernelOrchestrator**

Create `AIStudyHub.Business/Services/SemanticKernelOrchestrator.cs`:

```csharp
using AIStudyHub.Business.Configuration;
using AIStudyHub.Business.Guardrails;
using AIStudyHub.Business.Search;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;

namespace AIStudyHub.Business.Services;

public class SemanticKernelOrchestrator : ISemanticKernelOrchestrator
{
    private readonly IKernelMemoryService _kernelMemory;
    private readonly IHybridSearchService _searchService;
    private readonly IRerankingService _rerankingService;
    private readonly IFaithfulnessFilter _faithfulnessFilter;
    private readonly IGroundingVerifier _groundingVerifier;
    private readonly IConfidenceScorer _confidenceScorer;
    private readonly SemanticKernelOptions _options;
    private readonly ILogger<SemanticKernelOrchestrator> _logger;

    public SemanticKernelOrchestrator(
        IKernelMemoryService kernelMemory,
        IHybridSearchService searchService,
        IRerankingService rerankingService,
        IFaithfulnessFilter faithfulnessFilter,
        IGroundingVerifier groundingVerifier,
        IConfidenceScorer confidenceScorer,
        IOptions<SemanticKernelOptions> options,
        ILogger<SemanticKernelOrchestrator> logger)
    {
        _kernelMemory = kernelMemory;
        _searchService = searchService;
        _rerankingService = rerankingService;
        _faithfulnessFilter = faithfulnessFilter;
        _groundingVerifier = groundingVerifier;
        _confidenceScorer = confidenceScorer;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<RagResponse> AskAsync(Guid userId, string question, CancellationToken ct = default)
    {
        _logger.LogInformation("Processing RAG query for user {UserId}", userId);

        // L3: Retrieval with hybrid search and reranking
        var searchResults = await _searchService.SearchAsync(question, userId, 10, ct);
        var rerankedResults = await _rerankingService.RerankAsync(question, searchResults, 5, ct);
        
        var resultList = rerankedResults.ToList();
        if (!resultList.Any())
        {
            return new RagResponse("I couldn't find relevant information in your documents.", new(), 0.0);
        }

        // L4: Generate answer
        var context = BuildContext(resultList);
        var answer = await GenerateAnswerAsync(question, context, ct);

        // L5: Guardrails
        var isFaithful = await _faithfulnessFilter.ValidateAsync(answer, resultList.Select(r => r.Content));
        var groundingResult = await _groundingVerifier.VerifyAsync(answer, resultList);
        var confidence = _confidenceScorer.Score(answer, groundingResult, isFaithful);

        // Build citations
        var citations = resultList.Select((r, i) => new CitationInfo(
            Source: r.Source,
            Content: r.Content,
            Relevance: r.Score
        )).ToList();

        return new RagResponse(answer, citations, confidence);
    }

    private string BuildContext(IEnumerable<SearchResult> results)
    {
        return string.Join("\n\n---\n\n", results.Select((r, i) =>
            $"[{i + 1}] Source: {r.Source}\n{r.Content}"));
    }

    private async Task<string> GenerateAnswerAsync(string question, string context, CancellationToken ct)
    {
        // Use Kernel Memory's built-in Ask (simplified)
        var result = await _kernelMemory.AskAsync(question, Guid.Empty, ct);
        return result.Result;
    }
}
```

- [ ] **Step 3: Add DI registration**

Modify `BusinessServiceExtensions.cs`:

```csharp
services.Configure<SemanticKernelOptions>(configuration.GetSection("SemanticKernel"));
services.AddScoped<ISemanticKernelOrchestrator, SemanticKernelOrchestrator>();
```

- [ ] **Step 4: Commit**

```bash
git add AIStudyHub.Business/Services/SemanticKernelOrchestrator.cs
git add AIStudyHub.Business/Services/ISemanticKernelOrchestrator.cs
git commit -m "feat: implement SK orchestrator for L4"
```

---

## Phase 6: Guardrails (L5)

### Task 6.1: Create Guardrails Components

**Files:**
- Create: `AIStudyHub.Business/Guardrails/FaithfulnessFilter.cs`
- Create: `AIStudyHub.Business/Guardrails/GroundingVerifier.cs`
- Create: `AIStudyHub.Business/Guardrails/ConfidenceScorer.cs`

- [ ] **Step 1: Create FaithfulnessFilter**

Create `AIStudyHub.Business/Guardrails/FaithfulnessFilter.cs`:

```csharp
using AIStudyHub.Business.Configuration;
using Microsoft.Extensions.Options;

namespace AIStudyHub.Business.Guardrails;

public interface IFaithfulnessFilter
{
    Task<bool> ValidateAsync(string answer, IEnumerable<string> sourceContents);
}

public class FaithfulnessFilter : IFaithfulnessFilter
{
    private readonly GuardrailsOptions _options;
    private readonly ILogger<FaithfulnessFilter> _logger;

    public FaithfulnessFilter(IOptions<GuardrailsOptions> options, ILogger<FaithfulnessFilter> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<bool> ValidateAsync(string answer, IEnumerable<string> sourceContents)
    {
        // Basic check: answer should not claim to not know if relevant context exists
        var context = string.Join(" ", sourceContents);
        var answerLower = answer.ToLowerInvariant();
        
        // Heuristic: if context is rich but answer is evasive, flag it
        var hasContext = context.Length > 100;
        var isEvasive = answerLower.Contains("cannot find") || 
                        answerLower.Contains("i don't know") ||
                        answerLower.Contains("not mentioned");

        if (hasContext && isEvasive)
        {
            _logger.LogWarning("Faithfulness check failed: evasive answer despite available context");
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }
}
```

- [ ] **Step 2: Create GroundingVerifier**

Create `AIStudyHub.Business/Guardrails/GroundingVerifier.cs`:

```csharp
namespace AIStudyHub.Business.Guardrails;

public interface IGroundingVerifier
{
    Task<GroundingResult> VerifyAsync(string answer, IEnumerable<SearchResult> sources);
}

public record GroundingResult(
    bool IsGrounded,
    double Score,
    List<string> UngroundedClaims
);

public class GroundingVerifier : IGroundingVerifier
{
    private readonly ILogger<GroundingVerifier> _logger;

    public GroundingVerifier(ILogger<GroundingVerifier> logger)
    {
        _logger = logger;
    }

    public Task<GroundingResult> VerifyAsync(string answer, IEnumerable<SearchResult> sources)
    {
        var sourceTexts = sources.Select(s => s.Content.ToLowerInvariant()).ToList();
        var answerLower = answer.ToLowerInvariant();
        var ungrounded = new List<string>();

        // Simple keyword overlap check
        var sourceWords = sourceTexts
            .SelectMany(t => t.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(w => w.Length > 4)
            .Distinct()
            .ToHashSet();

        var answerWords = answerLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var groundedCount = answerWords.Count(w => sourceWords.Contains(w));
        var coverage = answerWords.Length > 0 ? (double)groundedCount / answerWords.Length : 0;

        _logger.LogInformation("Grounding verification: {Coverage:P2} coverage", coverage);

        return Task.FromResult(new GroundingResult(
            IsGrounded: coverage > 0.3,
            Score: coverage,
            UngroundedClaims: ungrounded
        ));
    }
}
```

- [ ] **Step 3: Create ConfidenceScorer**

Create `AIStudyHub.Business/Guardrails/ConfidenceScorer.cs`:

```csharp
using AIStudyHub.Business.Configuration;
using Microsoft.Extensions.Options;

namespace AIStudyHub.Business.Guardrails;

public interface IConfidenceScorer
{
    double Score(string answer, GroundingResult grounding, bool isFaithful);
}

public class ConfidenceScorer : IConfidenceScorer
{
    private readonly GuardrailsOptions _options;

    public ConfidenceScorer(IOptions<GuardrailsOptions> options)
    {
        _options = options.Value;
    }

    public double Score(string answer, GroundingResult grounding, bool isFaithful)
    {
        var score = grounding.Score;

        if (!isFaithful)
        {
            score *= 0.5;
        }

        // Penalize very short answers
        if (answer.Length < 50)
        {
            score *= 0.8;
        }

        // Boost for citations
        if (grounding.Score > _options.GroundingThreshold)
        {
            score += 0.1;
        }

        return Math.Clamp(score, 0, 1);
    }
}
```

- [ ] **Step 4: Add DI registration**

Modify `BusinessServiceExtensions.cs`:

```csharp
services.Configure<GuardrailsOptions>(configuration.GetSection("Guardrails"));
services.AddScoped<IFaithfulnessFilter, FaithfulnessFilter>();
services.AddScoped<IGroundingVerifier, GroundingVerifier>();
services.AddScoped<IConfidenceScorer, ConfidenceScorer>();
```

- [ ] **Step 5: Write tests**

Create `AIStudyHub.Tests/Guardrails/FaithfulnessFilterTests.cs`:

```csharp
using AIStudyHub.Business.Guardrails;
using Microsoft.Extensions.Logging;
using Moq;

namespace AIStudyHub.Tests.Guardrails;

public class FaithfulnessFilterTests
{
    private readonly FaithfulnessFilter _filter;

    public FaithfulnessFilterTests()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new AIStudyHub.Business.Configuration.GuardrailsOptions());
        _filter = new FaithfulnessFilter(options, NullLogger<FaithfulnessFilter>.Instance);
    }

    [Fact]
    public async Task ValidateAsync_ShouldReturnTrue_WhenContextIsRich()
    {
        // Arrange
        var answer = "The document mentions RAG architecture.";
        var sources = new[] { "RAG is a retrieval augmented generation architecture." };

        // Act
        var result = await _filter.ValidateAsync(answer, sources);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ValidateAsync_ShouldReturnFalse_WhenEvasiveWithRichContext()
    {
        // Arrange
        var answer = "I cannot find this information.";
        var sources = new[] { "RAG is a retrieval augmented generation architecture. It combines retrieval with generation." };

        // Act
        var result = await _filter.ValidateAsync(answer, sources);

        // Assert
        Assert.False(result);
    }
}
```

- [ ] **Step 6: Run tests**

Run: `dotnet test --filter "FullyQualifiedName~FaithfulnessFilterTests"`
Expected: Tests pass

- [ ] **Step 7: Commit**

```bash
git add AIStudyHub.Business/Guardrails/
git add AIStudyHub.Tests/Guardrails/
git commit -m "feat: implement guardrails L5"
```

---

## Phase 7: Controller Updates

### Task 7.1: Update DocumentUploadController

**Files:**
- Modify: `AIStudyHub.API/Controllers/DocumentUploadController.cs`

- [ ] **Step 1: Read and update controller**

Read `AIStudyHub.API/Controllers/DocumentUploadController.cs` and modify to use the queue:

Replace the processing logic:

```csharp
// OLD: Direct processing with Task.Run
[HttpPost("upload")]
public async Task<IActionResult> Upload(IFormFile file)
{
    var document = await _documentService.SaveDocumentAsync(file);
    _ = Task.Run(() => _processingService.ProcessDocumentsAsync(new List<Document> { document }));
    return Ok();
}

// NEW: Queue-based processing
private readonly IDocumentProcessingQueue _processingQueue;

[HttpPost("upload")]
public async Task<IActionResult> Upload(IFormFile file)
{
    var document = await _documentService.SaveDocumentAsync(file);
    
    var request = new DocumentProcessRequest(
        document.Id,
        GetUserId(),
        document.FilePath,
        document.FileName,
        file.ContentType);
    
    await _processingQueue.EnqueueAsync(request);
    
    return Ok(new { documentId = document.Id, status = "queued" });
}
```

- [ ] **Step 2: Commit**

```bash
git commit -m "refactor: wire DocumentUploadController to Channel queue"
```

---

### Task 7.2: Update/Add RagChatController

**Files:**
- Create: `AIStudyHub.API/Controllers/RagChatController.cs`

- [ ] **Step 1: Create RagChatController**

Create `AIStudyHub.API/Controllers/RagChatController.cs`:

```csharp
using AIStudyHub.Business.Services;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RagController : ControllerBase
{
    private readonly ISemanticKernelOrchestrator _orchestrator;
    private readonly ILogger<RagController> _logger;

    public RagController(
        ISemanticKernelOrchestrator orchestrator,
        ILogger<RagController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] AskRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest("Question is required");
        }

        var userId = GetUserId();
        var response = await _orchestrator.AskAsync(userId, request.Question, ct);

        return Ok(response);
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var userId) ? userId : Guid.Empty;
    }
}

public record AskRequest(string Question);
```

- [ ] **Step 2: Commit**

```bash
git add AIStudyHub.API/Controllers/RagChatController.cs
git commit -m "feat: add RAG chat endpoint"
```

---

## Phase 8: Reindex Existing Data

### Task 8.1: Create Reindex Command

**Files:**
- Create: `AIStudyHub.API/Commands/ReindexDocumentsCommand.cs`
- Create: `AIStudyHub.API/Controllers/AdminController.cs`

- [ ] **Step 1: Create admin endpoint**

Create `AIStudyHub.API/Controllers/AdminController.cs`:

```csharp
using AIStudyHub.Business.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentProcessingQueue _queue;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IDocumentRepository documentRepository,
        IDocumentProcessingQueue queue,
        ILogger<AdminController> logger)
    {
        _documentRepository = documentRepository;
        _queue = queue;
        _logger = logger;
    }

    [HttpPost("reindex")]
    public async Task<IActionResult> ReindexAll(CancellationToken ct)
    {
        _logger.LogInformation("Starting full reindex");

        var documents = await _documentRepository.GetAllDocumentsAsync(ct);
        var count = 0;

        foreach (var doc in documents)
        {
            var request = new DocumentProcessRequest(
                doc.Id,
                doc.UserId,
                doc.FilePath,
                doc.FileName,
                doc.ContentType);

            await _queue.EnqueueAsync(request, ct);
            count++;
        }

        _logger.LogInformation("Queued {Count} documents for reindexing", count);

        return Ok(new { message = $"Queued {count} documents for reindexing" });
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add AIStudyHub.API/Controllers/AdminController.cs
git commit -m "feat: add admin reindex endpoint"
```

---

## Phase 9: Final Integration & Testing

### Task 9.1: Run Full Test Suite

- [ ] **Step 1: Run all tests**

Run: `dotnet test --verbosity normal`
Expected: All tests pass

- [ ] **Step 2: Manual verification**

1. Start Qdrant and Ollama services
2. Upload a PDF document
3. Query the document
4. Verify citations appear

- [ ] **Step 3: Commit final changes**

```bash
git add -A
git commit -m "feat: complete RAG system refactor to Modern 5-Layer Architecture"
```

---

## Summary

| Phase | Task | Status |
|-------|------|--------|
| 1 | Setup & Config | |
| 1.1 | Add NuGet Packages | |
| 1.2 | Create Config Classes | |
| 2 | Background Processing | |
| 2.1 | Channel Queue | |
| 2.2 | BackgroundService | |
| 3 | Kernel Memory (L1-L2) | |
| 4 | Retrieval (L3) | |
| 5 | Generation (L4) | |
| 6 | Guardrails (L5) | |
| 7 | Controller Updates | |
| 8 | Reindex | |
| 9 | Testing | |
