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
        var realTimeNotifier = services.GetService<IRealTimeNotificationService>();

        try
        {
            var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
            var isTextDocument = new[] { ".pdf", ".docx", ".txt", ".md" }.Contains(extension);
            var isImageFile = new[] { ".jpg", ".png", ".jpeg", ".webp", ".gif"}.Contains(extension);


           if (isTextDocument)
{
    var fileContent = await System.IO.File.ReadAllBytesAsync(request.FilePath, ct);
    var documentProcessing = services.GetRequiredService<IDocumentProcessingService>();
    
    // Detect: scanned PDF → OCR, text PDF → Kernel Memory
    if (extension == ".pdf" && documentProcessing.IsScannedPdf(fileContent))
    {
        // === SCANNED PDF: OCR FLOW ===
        logger.LogInformation("Document {DocumentId}: Detected as scanned PDF, using OCR", 
            request.DocumentId);

        var ocrText = await documentProcessing.ExtractTextAsync(fileContent, extension);
        
        if (string.IsNullOrWhiteSpace(ocrText) || ocrText.Length < 10)
        {
            throw new InvalidOperationException(
                $"OCR extracted insufficient text ({ocrText?.Length ?? 0} chars). " +
                "PDF may be encrypted or contain no readable content.");
        }

        logger.LogInformation("Document {DocumentId}: OCR extracted {TextLength} chars",
            request.DocumentId, ocrText.Length);

        var chunks = await documentProcessing.ChunkTextAsync(ocrText, 1024, 128);
        logger.LogInformation("Document {DocumentId}: Split into {ChunkCount} chunks",
            request.DocumentId, chunks.Count);

        var sparseGen = services.GetRequiredService<ISparseVectorGenerator>();
        var qdrant = services.GetRequiredService<IVectorStoreService>();
        var embeddingService = services.GetRequiredService<IEmbeddingService>();

        await qdrant.EnsureCollectionExistsAsync();

        for (int i = 0; i < chunks.Count; i++)
        {
            var chunkText = chunks[i];
            if (string.IsNullOrWhiteSpace(chunkText)) continue;

            var dense = await embeddingService.GenerateEmbeddingAsync(chunkText);
            var sparse = sparseGen.GenerateSparseVector(chunkText);

            var metadata = new Dictionary<string, string>
            {
                { "documentId", request.DocumentId.ToString() },
                { "userId", request.UserId.ToString() },
                { "text", chunkText },
                { "fileName", request.FileName },
                { "chunkIndex", i.ToString() }
            };

            await qdrant.UpsertVectorAsync(Guid.NewGuid().ToString(), dense, sparse, metadata);
        }

        logger.LogInformation("Document {DocumentId}: Scanned PDF processed. {ChunkCount} chunks upserted",
            request.DocumentId, chunks.Count);
    }
    else
    {
        // === TEXT PDF / DOCX: KERNEL MEMORY FLOW ===
        await kernelMemoryService.ImportDocumentAsync(
            request.FilePath,
            request.DocumentId,
            request.UserId,
            request.FileName,
            ct);

        logger.LogInformation("Document {DocumentId}: Fetching chunks from Kernel Memory", 
            request.DocumentId);
        var chunks = await kernelMemoryService.SearchAsync("", request.UserId, 1000, ct);

        var sparseGen = services.GetRequiredService<ISparseVectorGenerator>();
        var qdrant = services.GetRequiredService<IVectorStoreService>();
        var embeddingService = services.GetRequiredService<IEmbeddingService>();

        await qdrant.EnsureCollectionExistsAsync();

        int chunkIndex = 0;
        int upsertedCount = 0;
        int skippedCount = 0;
        string? firstCitationDocId = null;

        foreach (var citation in chunks)
        {
            if (citation.DocumentId != request.DocumentId.ToString())
            {
                skippedCount++;
                firstCitationDocId ??= citation.DocumentId;
                continue;
            }

            foreach (var partition in citation.Partitions)
            {
                var text = partition.Text;
                if (string.IsNullOrWhiteSpace(text)) continue;

                var dense = await embeddingService.GenerateEmbeddingAsync(text);
                var sparse = sparseGen.GenerateSparseVector(text);

                var metadata = new Dictionary<string, string>
                {
                    { "documentId", request.DocumentId.ToString() },
                    { "userId", request.UserId.ToString() },
                    { "text", text },
                    { "fileName", request.FileName },
                    { "chunkIndex", chunkIndex.ToString() }
                };

                await qdrant.UpsertVectorAsync(Guid.NewGuid().ToString(), dense, sparse, metadata);
                chunkIndex++;
                upsertedCount++;
            }
        }

        logger.LogInformation("Document {DocumentId}: Upserted {Upserted} chunks to Qdrant",
            request.DocumentId, upsertedCount);
    }
}
            else if (isImageFile)
            {
                logger.LogInformation("Document {DocumentId}: Processing image file {FileName} via OCR",
                    request.DocumentId, request.FileName);

                var documentProcessing = services.GetRequiredService<IDocumentProcessingService>();
                var sparseGen = services.GetRequiredService<ISparseVectorGenerator>();
                var qdrant = services.GetRequiredService<IVectorStoreService>();
                var embeddingService = services.GetRequiredService<IEmbeddingService>();

                var fileContent = await System.IO.File.ReadAllBytesAsync(request.FilePath, ct);

                var text = await documentProcessing.ExtractTextAsync(fileContent, extension);
                if (string.IsNullOrWhiteSpace(text) || text.Length < 10)
                {
                    throw new InvalidOperationException(
                        $"OCR extracted insufficient text ({text?.Length ?? 0} chars). "
                        + "Image may be empty, blurred, or contain only graphics.");
                }

                logger.LogInformation("Document {DocumentId}: OCR extracted {TextLength} chars from image",
                    request.DocumentId, text.Length);

                var chunks = await documentProcessing.ChunkTextAsync(text, 1024, 128);
                logger.LogInformation("Document {DocumentId}: Split into {ChunkCount} chunks",
                    request.DocumentId, chunks.Count);

                await qdrant.EnsureCollectionExistsAsync();

                for (int i = 0; i < chunks.Count; i++)
                {
                    var chunkText = chunks[i];
                    if (string.IsNullOrWhiteSpace(chunkText)) continue;

                    var dense = await embeddingService.GenerateEmbeddingAsync(chunkText);
                    var sparse = sparseGen.GenerateSparseVector(chunkText);

                    var metadata = new Dictionary<string, string>
                    {
                        { "documentId", request.DocumentId.ToString() },
                        { "userId", request.UserId.ToString() },
                        { "text", chunkText },
                        { "fileName", request.FileName },
                        { "chunkIndex", i.ToString() }
                    };

                    await qdrant.UpsertVectorAsync(Guid.NewGuid().ToString(), dense, sparse, metadata);
                }

                logger.LogInformation("Document {DocumentId}: OCR image processing complete. "
                    + "{ChunkCount} chunks upserted to Qdrant", request.DocumentId, chunks.Count);

                var imageDoc = await unitOfWork.Documents.GetByIdAsync(request.DocumentId, ct);
                if (imageDoc != null)
                {
                    imageDoc.IsOcrApplied = true;
                }
            }
            else
            {
                logger.LogInformation("Document {DocumentId} is a media file ({Extension}), skipping vectorization",
                    request.DocumentId, extension);
            }

            // Update document status in database
            var document = await unitOfWork.Documents.GetByIdAsync(request.DocumentId, ct);
            if (document != null)
            {
                document.Status = DocumentStatus.Done;
                document.ErrorMessage = null;
                document.UpdatedAt = DateTime.UtcNow;
                unitOfWork.Documents.Update(document);
                await unitOfWork.SaveChangesAsync(ct);
            }

            logger.LogInformation("Document {DocumentId} processed and indexed successfully", request.DocumentId);

            // Real-time push to notify user the document is ready
            if (realTimeNotifier is not null)
            {
                try
                {
                    await realTimeNotifier.NotifyDocumentProcessedAsync(
                        request.UserId, request.DocumentId, request.FileName, ct);
                }
                catch (Exception notifyEx)
                {
                    logger.LogWarning(notifyEx, "Real-time notify (document processed) failed for document {DocumentId}", request.DocumentId);
                }
            }

            // Plan C3: check Bookworm badge after the doc is marked Done.
            var badgeService = services.GetService<AIStudyHub.Business.Interfaces.Services.IBadgeService>();
            if (badgeService is not null && document != null)
            {
                try
                {
                    var unlocked = await badgeService.EvaluateDocumentBadgeAsync(request.UserId, ct);
                    if (unlocked.Count > 0)
                    {
                        logger.LogInformation("Unlocked {Count} document badge(s) for user {UserId}", unlocked.Count, request.UserId);
                    }
                }
                catch (Exception badgeEx)
                {
                    logger.LogWarning(badgeEx, "Badge evaluation failed for user {UserId} on document {DocumentId}", request.UserId, request.DocumentId);
                }
            }

            // Spec v4.0 / Module 2 (Pure REST API): no SignalR. Frontend re-fetches
            // /api/Document/{id} or /api/Document?status=Failed after navigation
            // and picks up the updated Status + ErrorMessage from SQL.
        }
        catch (Exception ex)
        {
            // Mark document as failed and persist the error message (Plan A.8)
            var document = await unitOfWork.Documents.GetByIdAsync(request.DocumentId, ct);
            if (document != null)
            {
                document.Status = DocumentStatus.Failed;
                document.ErrorMessage = $"{ex.GetType().Name}: {ex.Message}";
                document.UpdatedAt = DateTime.UtcNow;
                unitOfWork.Documents.Update(document);
                await unitOfWork.SaveChangesAsync(ct);
            }

            logger.LogError(ex, "Failed to process document {DocumentId}", request.DocumentId);

            // Real-time push to notify user the document failed
            if (realTimeNotifier is not null)
            {
                try
                {
                    await realTimeNotifier.NotifyDocumentFailedAsync(
                        request.UserId, request.DocumentId, request.FileName, ex.Message, ct);
                }
                catch (Exception notifyEx)
                {
                    logger.LogWarning(notifyEx, "Real-time notify (document failed) failed for document {DocumentId}", request.DocumentId);
                }
            }
        }
    }

    private Task HandleFailureAsync(DocumentProcessRequest request, Exception ex)
    {
        _logger.LogWarning("Document {DocumentId} moved to dead-letter: {Error}",
            request.DocumentId, ex.Message);
        return Task.CompletedTask;
    }
}
