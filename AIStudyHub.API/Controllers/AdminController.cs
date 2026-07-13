using AIStudyHub.Business.Services;
using AIStudyHub.Data.Interfaces;
using AIStudyHub.Business.DTOs.Documents;
using AIStudyHub.Business.Interfaces.AI.VectorStore;
using AIStudyHub.Business.Interfaces.AI.Search;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDocumentProcessingQueue _queue;
    private readonly ILogger<AdminController> _logger;
    private readonly IDocumentProcessingService _docProcessing;
    private readonly IEmbeddingService _embedding;
    private readonly ISparseVectorGenerator _sparse;
    private readonly IVectorStoreService _qdrant;
    private readonly IFileStorageService _storage;
    private readonly IOptions<RagOptions> _ragOptions;

    public AdminController(
        IUnitOfWork unitOfWork,
        IDocumentProcessingQueue queue,
        ILogger<AdminController> logger,
        IDocumentProcessingService docProcessing,
        IEmbeddingService embedding,
        ISparseVectorGenerator sparse,
        IVectorStoreService qdrant,
        IFileStorageService storage,
        IOptions<RagOptions> ragOptions)
    {
        _unitOfWork = unitOfWork;
        _queue = queue;
        _logger = logger;
        _docProcessing = docProcessing;
        _embedding = embedding;
        _sparse = sparse;
        _qdrant = qdrant;
        _storage = storage;
        _ragOptions = ragOptions;
    }

    [HttpPost("reindex")]
    public async Task<IActionResult> ReindexAll(CancellationToken ct)
    {
        _logger.LogInformation("Starting full reindex by admin");

        var documents = await _unitOfWork.Documents.GetAllAsync(ct);
        var count = 0;

        foreach (var doc in documents)
        {
            var request = new DocumentProcessRequest(
                doc.Id,
                doc.UserId,
                doc.FileLink ?? string.Empty,
                doc.FileName ?? "unknown",
                doc.FileType ?? "application/octet-stream");

            await _queue.EnqueueAsync(request, ct);
            count++;
        }

        _logger.LogInformation("Queued {Count} documents for reindexing", count);

        return Ok(new 
        { 
            message = $"Queued {count} documents for reindexing",
            count = count
        });
    }

    [HttpPost("reindex-tables/{documentId:guid}")]
    public async Task<IActionResult> ReindexTables(Guid documentId, CancellationToken ct)
    {
        _logger.LogInformation("Table-preserving reindex started for document {DocumentId}", documentId);

        var doc = await _unitOfWork.Documents.GetByIdAsync(documentId, ct);
        if (doc == null)
            return NotFound(new { message = "Document not found" });

        if (string.IsNullOrEmpty(doc.FileLink))
            return BadRequest(new { message = "Document has no file path" });

        var baseDir = Path.GetFullPath(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "uploads"));
        var filePath = Path.Combine(baseDir, doc.FileLink);

        if (!System.IO.File.Exists(filePath))
            return NotFound(new { message = "File not found on disk", path = filePath });

        var fileContent = await System.IO.File.ReadAllBytesAsync(filePath, ct);
        var extension = Path.GetExtension(doc.FileName ?? "").ToLowerInvariant();
        var rawText = await _docProcessing.ExtractTextAsync(fileContent, extension);

        if (string.IsNullOrWhiteSpace(rawText) || rawText.Length < 10)
            return BadRequest(new { message = $"Extracted text too short ({rawText?.Length ?? 0} chars)" });

        _logger.LogInformation("Document {DocumentId}: extracted {CharCount} chars, rebuilding with preserveTables=true", documentId, rawText.Length);

        var chunks = await _docProcessing.ChunkTextAsync(
            rawText, _ragOptions.Value.ChunkSize, _ragOptions.Value.ChunkOverlap, preserveTables: true);

        var validChunks = chunks.Where(c => !string.IsNullOrWhiteSpace(c.Text)).ToList();
        if (validChunks.Count == 0)
            return BadRequest(new { message = "No valid chunks generated" });

        await _qdrant.DeleteVectorsByDocumentIdAsync(documentId);
        _logger.LogInformation("Document {DocumentId}: deleted old vectors", documentId);

        await _qdrant.EnsureCollectionExistsAsync();
        
        var chunkTexts = validChunks.Select(c => c.Text).ToList();
        var denseVectors = await _embedding.GenerateEmbeddingsAsync(chunkTexts);

        for (int i = 0; i < validChunks.Count; i++)
        {
            var chunk = validChunks[i];
            var sparse = _sparse.GenerateSparseVector(chunk.Text);
            var metadata = new Dictionary<string, string>
            {
                { "documentId", documentId.ToString() },
                { "userId", doc.UserId.ToString() },
                { "text", chunk.Text },
                { "fileName", doc.FileName ?? "" },
                { "chunkIndex", i.ToString() }
            };
            if (chunk.PageNumber.HasValue)
            {
                metadata.Add("pageNumber", chunk.PageNumber.Value.ToString());
            }
            await _qdrant.UpsertVectorAsync(Guid.NewGuid().ToString(), denseVectors[i], sparse, metadata);
        }

        _logger.LogInformation(
            "Document {DocumentId}: reindexed with {ChunkCount} table-preserving chunks",
            documentId, validChunks.Count);

        return Ok(new
        {
            message = $"Reindexed with table-preserving chunks",
            documentId = documentId,
            chunkCount = validChunks.Count,
            charCount = rawText.Length
        });
    }
}
