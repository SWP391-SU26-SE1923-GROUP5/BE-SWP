using AIStudyHub.API.DTOs;
using AIStudyHub.Business.AI.VectorStore;
using AIStudyHub.Business.DTOs.Documents;
using AIStudyHub.Business.DTOs.Rag;
using AIStudyHub.Business.Interfaces.AI.VectorStore;
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

namespace AIStudyHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class DocumentController : ControllerBase
{
    private readonly IDocumentService _service;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDocumentProcessingService _documentProcessing;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStoreService _vectorStoreService;
    private readonly IFileStorageService _fileStorage;
    private readonly RagOptions _ragOptions;
    private readonly DocumentStorageOptions _storageOptions;
    private readonly ILogger<DocumentController> _logger;
    private readonly IDocumentProcessingQueue _processingQueue;

    public DocumentController(
        IDocumentService service,
        IUnitOfWork unitOfWork,
        IDocumentProcessingService documentProcessing,
        IEmbeddingService embeddingService,
        IVectorStoreService vectorStoreService,
        IFileStorageService fileStorage,
        IOptions<RagOptions> ragOptions,
        IOptions<DocumentStorageOptions> storageOptions,
        ILogger<DocumentController> logger,
        IDocumentProcessingQueue processingQueue)
    {
        _service = service;
        _unitOfWork = unitOfWork;
        _documentProcessing = documentProcessing;
        _embeddingService = embeddingService;
        _vectorStoreService = vectorStoreService;
        _fileStorage = fileStorage;
        _ragOptions = ragOptions.Value;
        _storageOptions = storageOptions.Value;
        _logger = logger;
        _processingQueue = processingQueue;
    }

    [HttpGet]
    public async Task<ActionResult<AIStudyHub.Business.DTOs.Common.PagedResultDto<DocumentResponseDto>>> GetAll(
        [FromQuery] AIStudyHub.Business.DTOs.Common.PaginationParams @params,
        [FromQuery] Guid? subjectId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await _service.GetAllPagedAsync(userId, @params, subjectId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DocumentResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        if (result == null) return NotFound();

        var userId = GetCurrentUserId();
        if (userId != Guid.Empty && result.UserId != userId && result.ShareStatus != "public")
        {
            var isShared = await _unitOfWork.DocumentShares
                .Query()
                .AnyAsync<DocumentShare>(s => s.DocumentId == id && s.UserId == userId, cancellationToken);
            if (!isShared) return Forbid();
        }

        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DocumentResponseDto>> Update(Guid id, [FromBody] UpdateDocumentRequestDto request, CancellationToken cancellationToken)
    {
        var document = await _service.GetByIdAsync(id, cancellationToken);
        if (document == null) return NotFound();

        var userId = GetCurrentUserId();
        if (document.UserId != userId) return Forbid();

        var result = await _service.UpdateAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/share")]
    public async Task<ActionResult<ShareDocumentResponseDto>> Share(
        Guid id,
        [FromBody] ShareDocumentRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await _service.ShareDocumentAsync(id, userId, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var document = await _unitOfWork.Documents.GetByIdAsync(id, cancellationToken);
        if (document == null) return NotFound();

        var userId = GetCurrentUserId();
        if (document.UserId != userId) return Forbid();

        // Soft-delete: move to trash instead of hard-delete.
        // Owner can still see it in /api/Document/trash and restore it.
        document.LifecycleStatus = DocumentLifecycleStatus.Trashed;
        document.TrashedAt = DateTime.UtcNow;
        document.TrashedBy = userId;
        _unitOfWork.Documents.Update(document);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Document {DocumentId} trashed by user {UserId}", id, userId);
        return NoContent();
    }

    /// <summary>Returns the calling user's trashed documents.</summary>
    [HttpGet("trashed")]
    public async Task<ActionResult<IReadOnlyList<DocumentResponseDto>>> GetTrash(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();
        var result = await _service.GetTrashAsync(userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>Restores a trashed document back to active state.</summary>
    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();
        await _service.RestoreAsync(id, userId, cancellationToken);
        return Ok();
    }

    /// <summary>Permanently purges a trashed document. Idempotent — returns 204 even if already purged.</summary>
    [HttpDelete("{id:guid}/purge")]
    public async Task<IActionResult> Purge(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();
        await _service.PurgeAsync(id, userId, cancellationToken);
        _logger.LogInformation("Document {DocumentId} permanently purged by user {UserId}", id, userId);
        return NoContent();
    }

    /// <summary>Lists all per-user share entries for a document. Owner only.</summary>
    [HttpGet("{id:guid}/shares")]
    public async Task<ActionResult<DocumentShareListDto>> GetShares(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();
        var result = await _service.GetSharesAsync(id, userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>Revokes a specific user's access to a shared document. Owner only.</summary>
    [HttpDelete("{documentId:guid}/shares/{targetUserId:guid}")]
    public async Task<IActionResult> RevokeShare(Guid documentId, Guid targetUserId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();
        await _service.RevokeShareAsync(documentId, targetUserId, userId, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var document = await _service.GetByIdAsync(id, cancellationToken);
        if (document is null) return NotFound();

        var userId = GetCurrentUserId();
        if (userId != Guid.Empty && document.UserId != userId && document.ShareStatus != "public")
        {
            var isShared = await _unitOfWork.DocumentShares
                .Query()
                .AnyAsync<DocumentShare>(s => s.DocumentId == id && s.UserId == userId, cancellationToken);
            if (!isShared) return Forbid();
        }

        if (string.IsNullOrEmpty(document.FileLink))
            return NotFound("No file associated with this document");

        var relativePath = document.FileLink.Replace("/uploads/", "");
        var fullPath = Path.GetFullPath(Path.Combine(_storageOptions.BasePath ?? string.Empty, relativePath));

        if (!System.IO.File.Exists(fullPath))
            return NotFound("File not found on disk");

        var contentType = document.FileType ?? GetMimeTypeFromExtension(relativePath);
        var fileName = document.FileName ?? Path.GetFileName(relativePath);

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return File(stream, contentType, fileName, enableRangeProcessing: true);
    }

    [HttpGet("{id:guid}/preview")]
    public async Task<IActionResult> Preview(Guid id, CancellationToken cancellationToken)
    {
        var document = await _service.GetByIdAsync(id, cancellationToken);
        if (document is null) return NotFound();

        var userId = GetCurrentUserId();
        if (userId != Guid.Empty && document.UserId != userId && document.ShareStatus != "public")
        {
            var isShared = await _unitOfWork.DocumentShares
                .Query()
                .AnyAsync<DocumentShare>(s => s.DocumentId == id && s.UserId == userId, cancellationToken);
            if (!isShared) return Forbid();
        }

        if (string.IsNullOrEmpty(document.FileLink))
            return NotFound("No file associated with this document");

        var relativePath = document.FileLink.Replace("/uploads/", "");
        var fullPath = Path.GetFullPath(Path.Combine(_storageOptions.BasePath ?? string.Empty, relativePath));

        if (!System.IO.File.Exists(fullPath))
            return NotFound("File not found on disk");

        var contentType = document.FileType ?? GetMimeTypeFromExtension(relativePath);
        var fileName = document.FileName ?? Path.GetFileName(relativePath);

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        Response.Headers.Append("Content-Disposition", $"inline; filename=\"{fileName}\"");
        return File(stream, contentType, enableRangeProcessing: true);
    }

    private static readonly Dictionary<string, string> MimeTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Video
        { ".mp4",  "video/mp4" },
        { ".webm", "video/webm" },
        { ".ogg",  "video/ogg" },
        { ".avi",  "video/x-msvideo" },
        { ".mkv",  "video/x-matroska" },
        { ".mov",  "video/quicktime" },
        { ".wmv",  "video/x-ms-wmv" },
        { ".flv",  "video/x-flv" },
        { ".m4v",  "video/x-m4v" },
        // Audio
        { ".mp3",  "audio/mpeg" },
        { ".wav",  "audio/wav" },
        { ".ogg",  "audio/ogg" },
        { ".aac",  "audio/aac" },
        { ".flac", "audio/flac" },
        { ".m4a",  "audio/mp4" },
        { ".wma",  "audio/x-ms-wma" },
        { ".opus", "audio/opus" },
        // Images
        { ".jpg",  "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".png",  "image/png" },
        { ".gif",  "image/gif" },
        { ".webp", "image/webp" },
        { ".svg",  "image/svg+xml" },
        { ".bmp",  "image/bmp" },
        { ".ico",  "image/x-icon" },
        { ".tiff", "image/tiff" },
        { ".tif",  "image/tiff" },
        // Text / PDF
        { ".pdf",  "application/pdf" },
        { ".txt",  "text/plain" },
        { ".html", "text/html" },
        { ".htm",  "text/html" },
        { ".css",  "text/css" },
        { ".js",   "application/javascript" },
        { ".json", "application/json" },
        { ".xml",  "application/xml" },
        { ".csv",  "text/csv" },
        // Office
        { ".doc",  "application/msword" },
        { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
        { ".xls",  "application/vnd.ms-excel" },
        { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
        { ".ppt",  "application/vnd.ms-powerpoint" },
        { ".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation" },
    };

    private static string GetMimeTypeFromExtension(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return MimeTypeMap.TryGetValue(ext, out var mime) ? mime : "application/octet-stream";
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

        var userId = GetCurrentUserId();
        if (document.UserId != userId)
            return Forbid();

        var payloads = await _vectorStoreService.GetPayloadsByDocumentIdAsync(id);

        var chunks = payloads.Select(p => new ChunkDto(
            Guid.NewGuid(),
            id,
            p.GetValueOrDefault("text", ""),
            int.TryParse(p.GetValueOrDefault("chunkIndex", "0"), out var idx) ? idx : 0,
            null
        )).OrderBy(c => c.OrderIndex).ToList();

        return Ok(chunks);
    }

    [HttpPost("upload/file")]
    [Consumes("multipart/form-data")]
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

        var subject = await _unitOfWork.Subjects.GetByIdAsync(request.SubjectId, cancellationToken);
        if (subject == null)
            return BadRequest($"Subject with ID {request.SubjectId} not found");

        if (request.File.Length > _ragOptions.MaxFileSizeBytes)
            return BadRequest($"File exceeds maximum allowed size of {_ragOptions.MaxFileSizeBytes / (1024 * 1024)}MB");

        try
        {
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
            user.CurrentStorageCapacity += (int)fileSizeMb;
            _unitOfWork.Users.Update(user);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Document {DocumentId} accepted for processing by user {UserId}", document.Id, userId);

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

    [HttpPost("{id:guid}/reprocess")]
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

        await _vectorStoreService.DeleteVectorsByDocumentIdAsync(id);

        document.Status = DocumentStatus.Processing;
        document.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Documents.Update(document);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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
}