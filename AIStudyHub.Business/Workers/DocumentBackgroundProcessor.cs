using AIStudyHub.Business.AI.VectorStore;
using AIStudyHub.Business.Interfaces.AI.VectorStore;
using AIStudyHub.Business.Interfaces.AI.Search;
using AIStudyHub.Business.Interfaces.AI.Orchestration;
using AIStudyHub.Business.AI.Orchestration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AIStudyHub.Business.Interfaces;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;
using AIStudyHub.Data.Interfaces;
using AIStudyHub.Business.Interfaces.AI.LLM;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.DTOs.Documents;
using AIStudyHub.Business.Services;
using AIStudyHub.Business.Options;
using AIStudyHub.Business.AI;

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

    private const int MaxSummaryPromptChars = 4000;
    private const int MaxSummaryLength = 1500;

    private static async Task<string> GenerateDocumentSummaryAsync(
        string documentText,
        IOpenAIService openAiService,
        ILogger logger,
        CancellationToken ct)
    {
        try
        {
            var truncated = documentText.Length > MaxSummaryPromptChars
                ? documentText[..MaxSummaryPromptChars] + "\n\n[...document truncated...]"
                : documentText;

            var prompt = $"Đọc nội dung sau và viết 1 đoạn tóm tắt ngắn (2-3 câu) bằng tiếng Việt, " +
                $"nêu rõ chủ đề chính và các mục quan trọng:\n\n{truncated}";

            var summary = await openAiService.SendMessageAsync(prompt);
            if (!string.IsNullOrWhiteSpace(summary))
            {
                logger.LogInformation("Document summary generated ({SummaryLength} chars)", summary.Length);
                var prefix = "[Tóm tắt tài liệu] ";
                if (summary.Length > MaxSummaryLength)
                    return prefix + summary[..MaxSummaryLength] + "...";
                return prefix + summary;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to generate document summary — skipping summary chunk");
        }
        return string.Empty;
    }

    private async Task EmbedChunksAsync(
        List<DocumentChunkDto> chunks,
        Guid documentId,
        Guid userId,
        string fileName,
        Guid indexRunId,
        IServiceProvider services,
        CancellationToken ct)
    {
        var validChunks = chunks.Where(c => !string.IsNullOrWhiteSpace(c.Text)).ToList();
        if (validChunks.Count == 0) return;
        var chunkTexts = validChunks.Select(c => c.Text).ToList();

        var sparseGen = services.GetRequiredService<ISparseVectorGenerator>();
        var qdrant = services.GetRequiredService<IVectorStoreService>();
        var embeddingService = services.GetRequiredService<IEmbeddingService>();

        await qdrant.EnsureCollectionExistsAsync();

        List<float[]> denseVectors = null!;
        const int maxRetries = 3;
        var delay = TimeSpan.FromSeconds(1);
        Exception? lastEx = null;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                denseVectors = await embeddingService.GenerateEmbeddingsAsync(chunkTexts);
                break;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                lastEx = ex;
                _logger.LogWarning(ex,
                    "Embedding attempt {Attempt}/{MaxRetries} failed, retrying in {Delay}s",
                    attempt, maxRetries, delay.TotalSeconds);
                await Task.Delay(delay, ct);
                delay *= 2;
            }
            catch (Exception ex)
            {
                lastEx = ex;
            }
        }

        if (denseVectors == null)
            throw lastEx ?? new InvalidOperationException("Embedding failed after retries");

        for (int i = 0; i < validChunks.Count; i++)
        {
            var chunk = validChunks[i];
            var chunkText = chunk.Text;
            var sparse = sparseGen.GenerateSparseVector(chunkText);
            var metadata = new Dictionary<string, string>
            {
                { "documentId", documentId.ToString() },
                { "userId", userId.ToString() },
                { "text", chunkText },
                { "fileName", fileName },
                { "chunkIndex", i.ToString() },
                { "contentType", chunk.ContentType.ToString() },
                { "isHighlightable", chunk.IsHighlightable.ToString() },
                { "processingVersion", DocumentIngestionVersion.Current.ToString() },
                { "indexRunId", indexRunId.ToString() }
            };
            if (chunk.PageNumber.HasValue)
            {
                metadata.Add("pageNumber", chunk.PageNumber.Value.ToString());
            }
            await qdrant.UpsertVectorAsync(Guid.NewGuid().ToString(), denseVectors[i], sparse, metadata);
        }
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

        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var logger = services.GetRequiredService<ILogger<DocumentBackgroundProcessor>>();
        var realTimeNotifier = services.GetService<IRealTimeNotificationService>();
        var indexRunId = request.IndexRunId ?? Guid.NewGuid();
        var indexed = false;

        try
        {
            var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
            var isTextDocument = new[] { ".pdf", ".docx", ".txt", ".md" }.Contains(extension);
            var isImageFile = new[] { ".jpg", ".png", ".jpeg", ".webp", ".gif" }.Contains(extension);

            var fileContent = await System.IO.File.ReadAllBytesAsync(request.FilePath, ct);
            var documentProcessing = services.GetRequiredService<IDocumentProcessingService>();
            var ragOptions = services.GetRequiredService<IOptions<RagOptions>>().Value;
            var openAiService = services.GetRequiredService<IOpenAIService>();

            // Detect scanned PDF before entering text-document flow
            if (extension == ".pdf" && documentProcessing.IsScannedPdf(fileContent))
            {
                // === SCANNED PDF: OCR FLOW ===
                logger.LogInformation("Document {DocumentId}: Detected as scanned PDF, using OCR",
                    request.DocumentId);

                var segments = await documentProcessing.ExtractSegmentsAsync(fileContent, extension);
                var ocrText = string.Join("\n", segments.Select(segment => segment.Text));

                if (string.IsNullOrWhiteSpace(ocrText) || ocrText.Length < 10)
                {
                    throw new InvalidOperationException(
                        $"OCR extracted insufficient text ({ocrText?.Length ?? 0} chars). " +
                        "PDF may be encrypted or contain no readable content.");
                }

                logger.LogInformation("Document {DocumentId}: OCR extracted {TextLength} chars",
                    request.DocumentId, ocrText.Length);

                var summaryChunk = await GenerateDocumentSummaryAsync(ocrText, openAiService, logger, ct);
                var chunks = await DocumentChunkAssembler.AssembleAsync(
                    documentProcessing, segments, summaryChunk,
                    ragOptions.ChunkSize, ragOptions.ChunkOverlap);

                logger.LogInformation("Document {DocumentId}: Split into {ChunkCount} chunks",
                    request.DocumentId, chunks.Count);

                await EmbedChunksAsync(chunks, request.DocumentId, request.UserId, request.FileName, indexRunId, services, ct);
                indexed = true;

                logger.LogInformation("Document {DocumentId}: Scanned PDF processed. {ChunkCount} chunks upserted",
                    request.DocumentId, chunks.Count);
            }
            else if (extension == ".docx")
            {
                // === DOCX: Extract text (paragraphs + tables + embedded image OCR) → chunk → Qdrant ===
                logger.LogInformation("Document {DocumentId}: Processing DOCX with ExtractTextAsync (includes tables + image OCR)",
                    request.DocumentId);

                var segments = await documentProcessing.ExtractSegmentsAsync(fileContent, extension);
                var docxText = string.Join("\n", segments.Select(segment => segment.Text));

                if (string.IsNullOrWhiteSpace(docxText) || docxText.Length < 10)
                {
                    throw new InvalidOperationException(
                        $"DOCX extraction returned insufficient text ({docxText?.Length ?? 0} chars). "
                        + "Document may be encrypted, contain only images, or be malformed.");
                }

                logger.LogInformation("Document {DocumentId}: DOCX extracted {TextLength} chars (text + tables + OCR'd images)",
                    request.DocumentId, docxText.Length);

                var summaryChunk = await GenerateDocumentSummaryAsync(docxText, openAiService, logger, ct);
                var chunks = await DocumentChunkAssembler.AssembleAsync(
                    documentProcessing, segments, summaryChunk,
                    ragOptions.ChunkSize, ragOptions.ChunkOverlap);

                logger.LogInformation("Document {DocumentId}: Split into {ChunkCount} chunks",
                    request.DocumentId, chunks.Count);

                await EmbedChunksAsync(chunks, request.DocumentId, request.UserId, request.FileName, indexRunId, services, ct);
                indexed = true;

                logger.LogInformation("Document {DocumentId}: DOCX processed. {ChunkCount} chunks upserted",
                    request.DocumentId, chunks.Count);
            }
            else if (isTextDocument)
            {
                // === TEXT PDF / TXT / MD: Direct extract → chunk → batch embed ===
                logger.LogInformation("Document {DocumentId}: Processing as text document with direct extraction",
                    request.DocumentId);

                var segments = await documentProcessing.ExtractSegmentsAsync(fileContent, extension);
                var text = string.Join("\n", segments.Select(segment => segment.Text));

                if (string.IsNullOrWhiteSpace(text) || text.Length < 10)
                {
                    throw new InvalidOperationException(
                        $"Text extraction returned insufficient content ({text?.Length ?? 0} chars). "
                        + "Document may be empty or corrupted.");
                }

                logger.LogInformation("Document {DocumentId}: Extracted {TextLength} chars",
                    request.DocumentId, text.Length);

                var summaryChunk = await GenerateDocumentSummaryAsync(text, openAiService, logger, ct);
                var chunks = await DocumentChunkAssembler.AssembleAsync(
                    documentProcessing, segments, summaryChunk,
                    ragOptions.ChunkSize, ragOptions.ChunkOverlap);

                logger.LogInformation("Document {DocumentId}: Split into {ChunkCount} chunks",
                    request.DocumentId, chunks.Count);

                await EmbedChunksAsync(chunks, request.DocumentId, request.UserId, request.FileName, indexRunId, services, ct);
                indexed = true;

                logger.LogInformation("Document {DocumentId}: Text document processed. {ChunkCount} chunks upserted",
                    request.DocumentId, chunks.Count);
            }
            else if (isImageFile)
            {
                logger.LogInformation("Document {DocumentId}: Processing image file {FileName} via OCR",
                    request.DocumentId, request.FileName);

                var segments = await documentProcessing.ExtractSegmentsAsync(fileContent, extension);
                var text = string.Join("\n", segments.Select(segment => segment.Text));
                if (string.IsNullOrWhiteSpace(text) || text.Length < 10)
                {
                    throw new InvalidOperationException(
                        $"OCR extracted insufficient text ({text?.Length ?? 0} chars). "
                        + "Image may be empty, blurred, or contain only graphics.");
                }

                logger.LogInformation("Document {DocumentId}: OCR extracted {TextLength} chars from image",
                    request.DocumentId, text.Length);

                var summaryChunk = await GenerateDocumentSummaryAsync(text, openAiService, logger, ct);
                var chunks = await DocumentChunkAssembler.AssembleAsync(
                    documentProcessing, segments, summaryChunk,
                    ragOptions.ChunkSize, ragOptions.ChunkOverlap);

                logger.LogInformation("Document {DocumentId}: Split into {ChunkCount} chunks",
                    request.DocumentId, chunks.Count);

                await EmbedChunksAsync(chunks, request.DocumentId, request.UserId, request.FileName, indexRunId, services, ct);
                indexed = true;

                logger.LogInformation("Document {DocumentId}: OCR image processing complete. "
                    + "{ChunkCount} chunks upserted to Qdrant", request.DocumentId, chunks.Count);

                var imageDoc = await unitOfWork.Documents.GetByIdAsync(request.DocumentId, ct);
                if (imageDoc != null)
                    imageDoc.IsOcrApplied = true;
            }
            else
            {
                logger.LogInformation("Document {DocumentId} is a media file ({Extension}), skipping vectorization",
                    request.DocumentId, extension);
            }

            var document = await unitOfWork.Documents.GetByIdAsync(request.DocumentId, ct);
            if (request.IsReindex
                && (document == null || !request.ReindexClaimId.HasValue
                    || document.ReindexClaimId != request.ReindexClaimId))
            {
                throw new InvalidOperationException("The document reindex claim is no longer current.");
            }

            if (indexed)
            {
                var vectorStore = services.GetRequiredService<IVectorStoreService>();
                await vectorStore.DeleteDocumentVectorsExceptRunAsync(request.DocumentId, indexRunId);
            }

            // Update document status in database
            if (document != null)
            {
                document.Status = DocumentStatus.Done;
                document.ErrorMessage = null;
                if (indexed)
                    document.ProcessingVersion = DocumentIngestionVersion.Current;
                document.ReindexClaimId = null;
                document.ReindexClaimedAt = null;
                document.LastReindexError = null;
                document.UpdatedAt = DateTime.UtcNow;
                unitOfWork.Documents.Update(document);
                await unitOfWork.SaveChangesAsync(ct);
            }

            logger.LogInformation("Document {DocumentId} processed and indexed successfully", request.DocumentId);

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

            var badgeService = services.GetService<AIStudyHub.Business.Interfaces.Services.IBadgeService>();
            if (badgeService is not null && document != null)
            {
                try
                {
                    var unlocked = await badgeService.EvaluateDocumentBadgeAsync(request.UserId, ct);
                    if (unlocked.Count > 0)
                        logger.LogInformation("Unlocked {Count} document badge(s) for user {UserId}", unlocked.Count, request.UserId);
                }
                catch (Exception badgeEx)
                {
                    logger.LogWarning(badgeEx, "Badge evaluation failed for user {UserId} on document {DocumentId}", request.UserId, request.DocumentId);
                }
            }
        }
        catch (Exception ex)
        {
            try
            {
                var vectorStore = services.GetRequiredService<IVectorStoreService>();
                await vectorStore.DeleteDocumentVectorsByRunAsync(request.DocumentId, indexRunId);
            }
            catch (Exception cleanupEx)
            {
                logger.LogWarning(cleanupEx,
                    "Could not remove incomplete vector run {IndexRunId} for document {DocumentId}",
                    indexRunId, request.DocumentId);
            }

            var document = await unitOfWork.Documents.GetByIdAsync(request.DocumentId, ct);
            if (document != null)
            {
                var error = $"{ex.GetType().Name}: {ex.Message}";
                if (request.IsReindex)
                {
                    // A reindex failure must not make the already-usable legacy document unavailable.
                    document.Status = DocumentStatus.Done;
                    if (request.ReindexClaimId.HasValue
                        && document.ReindexClaimId == request.ReindexClaimId)
                    {
                        document.ReindexClaimId = null;
                        document.ReindexClaimedAt = null;
                        document.LastReindexError = error.Length > 2000 ? error[..2000] : error;
                    }
                }
                else
                {
                    document.Status = DocumentStatus.Failed;
                    document.ErrorMessage = error;
                }
                document.UpdatedAt = DateTime.UtcNow;
                unitOfWork.Documents.Update(document);
                await unitOfWork.SaveChangesAsync(ct);
            }

            logger.LogError(ex, "Failed to process document {DocumentId}", request.DocumentId);

            if (!request.IsReindex && realTimeNotifier is not null)
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
