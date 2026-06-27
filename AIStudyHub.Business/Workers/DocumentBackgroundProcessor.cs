using AIStudyHub.Business.AI.VectorStore;
using AIStudyHub.Business.Interfaces.AI.VectorStore;
using AIStudyHub.Business.Interfaces.AI.Search;
using AIStudyHub.Business.Interfaces.AI.Orchestration;
using AIStudyHub.Business.AI.Orchestration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AIStudyHub.Business.Interfaces;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;
using AIStudyHub.Data.Interfaces;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.DTOs.Documents;
using AIStudyHub.Business.Services;

namespace AIStudyHub.Business.Workers;

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
        var services = scope.ServiceProvider;
        
        var kernelMemoryService = services.GetRequiredService<IKernelMemoryService>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var logger = services.GetRequiredService<ILogger<DocumentBackgroundProcessor>>();

        try
        {
            var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
            var isTextDocument = new[] { ".pdf", ".docx", ".txt", ".md" }.Contains(extension);

            if (isTextDocument)
            {
                // Import document to Kernel Memory (handles L1-L2: chunking, embedding, indexing)
                await kernelMemoryService.ImportDocumentAsync(
                    request.FilePath,
                    request.DocumentId,
                    request.UserId,
                    request.FileName,
                    ct);

                // Fetch the generated chunks from KernelMemory to populate our Custom Hybrid Search Collection
                var chunks = await kernelMemoryService.SearchAsync("", request.UserId, 1000, ct);

                var sparseGen = services.GetRequiredService<ISparseVectorGenerator>();
                var qdrant = services.GetRequiredService<IVectorStoreService>();
                var embeddingService = services.GetRequiredService<IEmbeddingService>();

                // Ensure our custom Hybrid collection is created and configured with Sparse vectors
                await qdrant.EnsureCollectionExistsAsync();

                int chunkIndex = 0;
                foreach (var citation in chunks)
                {
                    if (citation.DocumentId != request.DocumentId.ToString()) continue;

                    foreach (var partition in citation.Partitions)
                    {
                        var text = partition.Text;
                        if (string.IsNullOrWhiteSpace(text)) continue;

                        // Generate both Dense and Sparse representations
                        var dense = await embeddingService.GenerateEmbeddingAsync(text);
                        var sparse = sparseGen.GenerateSparseVector(text);

                        var id = Guid.NewGuid().ToString();
                        var metadata = new Dictionary<string, string>
                        {
                            { "documentId", request.DocumentId.ToString() },
                            { "userId", request.UserId.ToString() },
                            { "text", text },
                            { "fileName", request.FileName },
                            { "chunkIndex", chunkIndex.ToString() }
                        };

                        await qdrant.UpsertVectorAsync(id, dense, sparse, metadata);
                        chunkIndex++;
                    }
                }
            }
            else
            {
                logger.LogInformation("Document {DocumentId} is a media file ({Extension}), skipping vectorization", request.DocumentId, extension);
            }

            // Update document status in database
            var document = await unitOfWork.Documents.GetByIdAsync(request.DocumentId, ct);
            if (document != null)
            {
                document.Status = DocumentStatus.Done;
                document.UpdatedAt = DateTime.UtcNow;
                unitOfWork.Documents.Update(document);
                await unitOfWork.SaveChangesAsync(ct);
            }

            logger.LogInformation("Document {DocumentId} processed and indexed successfully", request.DocumentId);
        }
        catch (Exception ex)
        {
            // Mark document as failed
            var document = await unitOfWork.Documents.GetByIdAsync(request.DocumentId, ct);
            if (document != null)
            {
                document.Status = DocumentStatus.Failed;
                document.UpdatedAt = DateTime.UtcNow;
                unitOfWork.Documents.Update(document);
                await unitOfWork.SaveChangesAsync(ct);
            }
            
            logger.LogError(ex, "Failed to process document {DocumentId}", request.DocumentId);
            throw;
        }
    }

    private Task HandleFailureAsync(DocumentProcessRequest request, Exception ex)
    {
        _logger.LogWarning("Document {DocumentId} moved to dead-letter: {Error}",
            request.DocumentId, ex.Message);
        return Task.CompletedTask;
    }
}
