using AIStudyHub.Business.AI.VectorStore;
using AIStudyHub.Business.Interfaces.AI.VectorStore;
using AIStudyHub.API.DTOs;
using AIStudyHub.API.Swagger;
using AIStudyHub.Business.DTOs.Documents;
using AIStudyHub.Business.DTOs.Rag;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Options;
using AIStudyHub.Business.Services;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;
using AIStudyHub.Data.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Annotations;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class DocumentUploadController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDocumentProcessingService _documentProcessing;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStoreService _vectorStoreService;
    private readonly IFileStorageService _fileStorage;
    private readonly RagOptions _options;
    private readonly ILogger<DocumentUploadController> _logger;
    private readonly DocumentStorageOptions _storageOptions;

    private readonly IDocumentProcessingQueue _processingQueue;

    public DocumentUploadController(
        IUnitOfWork unitOfWork,
        IDocumentProcessingService documentProcessing,
        IEmbeddingService embeddingService,
        IVectorStoreService vectorStoreService,
        IFileStorageService fileStorage,
        IOptions<RagOptions> options,
        ILogger<DocumentUploadController> logger,
        IDocumentProcessingQueue processingQueue,
        IOptions<DocumentStorageOptions> storageOptions)
    {
        _unitOfWork = unitOfWork;
        _documentProcessing = documentProcessing;
        _embeddingService = embeddingService;
        _vectorStoreService = vectorStoreService;
        _fileStorage = fileStorage;
        _options = options.Value;
        _logger = logger;
        _processingQueue = processingQueue;
        _storageOptions = storageOptions.Value;
    }

    // POST /api/DocumentUpload/upload (Base64) đã bị xóa - dùng POST /api/DocumentUpload/upload/file (multipart/form-data) thay thế

    [HttpPost("upload/file")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(UploadDocumentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UploadDocumentResponseDto>> UploadDocumentFile(
        [FromForm] UploadDocumentFileRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.File == null || request.File.Length == 0)
            return BadRequest("No file provided");

        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("Document title is required");

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        // Validate SubjectId exists
        var subject = await _unitOfWork.Subjects.GetByIdAsync(request.SubjectId, cancellationToken);
        if (subject == null)
            return BadRequest($"Subject with ID {request.SubjectId} not found");

        // Check file size limit from options
        if (request.File.Length > _options.MaxFileSizeBytes)
            return BadRequest($"File exceeds maximum allowed size of {_options.MaxFileSizeBytes / (1024 * 1024)}MB");

        try
        {
            // Check storage quota
            var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
            if (user == null)
                return Unauthorized();

            var tier = await _unitOfWork.TierMemberships.GetByIdAsync(user.TierId, cancellationToken);
            if (tier == null)
                return StatusCode(500, "User tier not found");

            var fileSizeMb = request.File.Length / (1024.0 * 1024.0);
            if (user.CurrentStorageCapacity + fileSizeMb > tier.StorageLimitMb)
                return StatusCode(403, $"Storage quota exceeded. Your tier ({tier.TierName}) allows {tier.StorageLimitMb}MB. Current usage: {user.CurrentStorageCapacity:F2}MB. This file: {fileSizeMb:F2}MB.");

            var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();

            if (!_fileStorage.IsValidExtension(extension))
            {
                return BadRequest($"File extension '{extension}' is not allowed. Allowed: .pdf, .docx, .txt, .md");
            }

            await using var memoryStream = new MemoryStream();
            await request.File.CopyToAsync(memoryStream, cancellationToken);
            var fileContent = memoryStream.ToArray();

            var filePath = await _fileStorage.SaveFileAsync(fileContent, Path.GetFileNameWithoutExtension(request.File.FileName), extension, cancellationToken);
            var fileUrl = _fileStorage.GetFileUrl(filePath);

            var document = new Document
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SubjectId = request.SubjectId,
                Title = request.Title,
                FileName = request.File.FileName,
                FileExtension = extension,
                FileType = request.File.ContentType,
                FileLink = fileUrl,
                FileSizeBytes = request.File.Length,
                ShareStatus = "private",
                Status = DocumentStatus.Processing,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Documents.AddAsync(document);

            // Update user's storage usage (already calculated above)
            user.CurrentStorageCapacity += (int)fileSizeMb;
            _unitOfWork.Users.Update(user);

            await _unitOfWork.SaveChangesAsync(cancellationToken); // Save Document first to get ID

            _logger.LogInformation("Document {DocumentId} accepted for processing by user {UserId}", document.Id, userId);

            // Queue document for background processing using Channel
            var fullPath = Path.GetFullPath(Path.Combine(_storageOptions.BasePath ?? string.Empty, filePath));
            var processRequest = new DocumentProcessRequest(
                document.Id,
                userId,
                fullPath,
                request.File.FileName,
                request.File.ContentType);
            await _processingQueue.EnqueueAsync(processRequest);

            return Accepted(new UploadDocumentResponseDto(
                document.Id,
                "processing",
                0,
                "Document is being processed in the background"
            ));
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload document file for user {UserId}", userId);
            return StatusCode(500, "An error occurred while processing the document");
        }
    }

    [HttpGet("{id:guid}/status")]
    public async Task<ActionResult> GetUploadStatus(Guid id, CancellationToken cancellationToken)
    {
        var document = await _unitOfWork.Documents.GetByIdAsync(id, cancellationToken);
        if (document == null)
            return NotFound("Document not found");

        var userId = GetCurrentUserId();
        if (document.UserId != userId)
            return Forbid();

        return Ok(new
        {
            document.Id,
            Status = document.Status.ToString()
        });
    }

    [HttpGet("{id:guid}/chunks")]
    public async Task<ActionResult<List<ChunkDto>>> GetDocumentChunks(Guid id, CancellationToken cancellationToken)
    {
        var document = await _unitOfWork.Documents.GetByIdAsync(id);
        if (document == null)
            return NotFound("Document not found");

        // Temporary workaround: since SQL DocumentChunks is deleted, fetch from Vector Store using empty query 
        // to retrieve up to 1000 chunks for this document.
        var dummyDense = new float[1536]; // Match Nomic dimensions
        var dummySparse = (Indices: new List<uint>(), Values: new List<float>());
        var filter = new Dictionary<string, string> { { "documentId", id.ToString() } };
        
        var qdrantResults = await _vectorStoreService.HybridSearchAsync(dummyDense, dummySparse, 1000, filter);

        var chunks = qdrantResults.Select((r, idx) => new ChunkDto(
            Guid.TryParse(r.Id, out var g) ? g : Guid.NewGuid(),
            id,
            r.Metadata.GetValueOrDefault("text", ""),
            idx,
            null
        )).ToList();

        return Ok(chunks);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid id, CancellationToken cancellationToken)
    {
        var document = await _unitOfWork.Documents.GetByIdAsync(id);
        if (document == null)
            return NotFound("Document not found");

        var userId = GetCurrentUserId();
        if (document.UserId != userId)
            return Forbid();

        try
        {
            await _vectorStoreService.DeleteVectorsByDocumentIdAsync(id);

            _unitOfWork.Documents.Remove(document);

            // Delete physical file
            if (!string.IsNullOrEmpty(document.FileLink))
            {
                var relativePath = document.FileLink.Replace("/uploads/", "");
                await _fileStorage.DeleteFileAsync(relativePath, cancellationToken);
            }

            // Update user's storage usage
            var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
            if (user != null)
            {
                var fileSizeMb = document.FileSizeBytes / (1024.0 * 1024.0);
                user.CurrentStorageCapacity = Math.Max(0, user.CurrentStorageCapacity - (int)fileSizeMb);
                _unitOfWork.Users.Update(user);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Document {DocumentId} deleted by user {UserId}", id, userId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete document {DocumentId}", id);
            return StatusCode(500, "An error occurred while deleting the document");
        }
    }

    [HttpPost("{id:guid}/reprocess")]
    [ProducesResponseType(typeof(UploadDocumentResponseDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UploadDocumentResponseDto>> Reprocess(
        Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var document = await _unitOfWork.Documents.GetByIdAsync(id, cancellationToken);
        if (document == null) return NotFound("Document not found");
        if (document.UserId != userId) return Forbid();

        if (string.IsNullOrEmpty(document.FileLink))
            return BadRequest("Document has no associated file on disk to re-process");

        var relativePath = document.FileLink.Replace("/uploads/", "");
        var fullPath = Path.Combine(_storageOptions.BasePath ?? string.Empty, relativePath);
        if (!System.IO.File.Exists(fullPath))
            return BadRequest("Source file is missing on disk; cannot re-process");

        byte[] fileContent = await System.IO.File.ReadAllBytesAsync(fullPath, cancellationToken);
        var extension = (document.FileExtension ?? Path.GetExtension(document.FileName ?? "")).ToLowerInvariant();

        await _vectorStoreService.DeleteVectorsByDocumentIdAsync(id);

        document.Status = DocumentStatus.Processing;
        document.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Documents.Update(document);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Queue for background processing using Channel
        var processRequest = new DocumentProcessRequest(
            document.Id,
            userId,
            fullPath,
            document.FileName ?? "unknown",
            document.FileType ?? "application/octet-stream");
        await _processingQueue.EnqueueAsync(processRequest);

        return Accepted(new UploadDocumentResponseDto(id, "processing", 0,
            "Re-processing in progress"));
    }



    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub")
            ?? User.FindFirst("userId");

        return claim != null && Guid.TryParse(claim.Value, out var userId)
            ? userId
            : Guid.Empty;
    }

    private static string GetFileType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".txt" => "text/plain",
            ".md" => "text/markdown",
            _ => "application/octet-stream"
        };
    }
}
