using AIStudyHub.API.DTOs;
using AIStudyHub.Business.DTOs.Documents;
using AIStudyHub.Business.DTOs.Rag;
using AIStudyHub.Business.Interfaces.AI.VectorStore;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Services;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;
using AIStudyHub.Data.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class DocumentController : ControllerBase
{
    private const string SuggestedPromptsWelcomeMessage =
        "Chào mừng bạn đến với AIStudyHub! Tôi có thể giúp bạn khám phá tài liệu này. "
        + "Hãy chọn một câu hỏi gợi ý bên dưới hoặc nhập câu hỏi của riêng bạn.";
    private readonly IDocumentService _service;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IVectorStoreService _vectorStoreService;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<DocumentController> _logger;
    private readonly IDocumentUploadService _uploadService;

    public DocumentController(
        IDocumentService service,
        IUnitOfWork unitOfWork,
        IVectorStoreService vectorStoreService,
        IFileStorageService fileStorage,
        ILogger<DocumentController> logger,
        IDocumentUploadService uploadService)
    {
        _service = service;
        _unitOfWork = unitOfWork;
        _vectorStoreService = vectorStoreService;
        _fileStorage = fileStorage;
        _logger = logger;
        _uploadService = uploadService;
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

    [HttpGet("{id:guid}/suggested-prompts")]
    public async Task<ActionResult<SuggestedPromptsResponseDto>> GetSuggestedPrompts(
        Guid id,
        CancellationToken cancellationToken)
    {
        var document = await _unitOfWork.Documents.GetByIdAsync(id, cancellationToken);
        if (document is null)
            return NotFound();

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        if (document.UserId != userId && document.ShareStatus != "public")
        {
            var isShared = await _unitOfWork.DocumentShares
                .Query()
                .AnyAsync<DocumentShare>(share =>
                    share.DocumentId == id && share.UserId == userId, cancellationToken);
            if (!isShared)
                return Forbid();
        }

        IReadOnlyList<string> prompts = [];
        if (!string.IsNullOrWhiteSpace(document.SuggestedPromptsJson))
        {
            try
            {
                prompts = JsonSerializer.Deserialize<List<string>>(document.SuggestedPromptsJson) ?? [];
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Invalid suggested prompt JSON for document {DocumentId}", id);
            }
        }

        return Ok(new SuggestedPromptsResponseDto(id, SuggestedPromptsWelcomeMessage, prompts));
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

        try
        {
            var result = await _service.ShareDocumentAsync(id, userId, request, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            if (ex.Message.Contains("User"))
                return BadRequest(new { message = ex.Message });
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
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

        await _vectorStoreService.DeleteVectorsByDocumentIdAsync(id);

        _logger.LogInformation("Document {DocumentId} trashed and vectors removed by user {UserId}", id, userId);
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
        try
        {
            await _service.RestoreAsync(id, userId, cancellationToken);
            return Ok();
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("unique document filename", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new { message = ex.Message });
        }
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

        if (!TryResolveStoredFilePath(
                document.FileLink,
                out var relativePath,
                out var fullPath))
        {
            return NotFound("File not found on disk");
        }

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

        if (!TryResolveStoredFilePath(
                document.FileLink,
                out var relativePath,
                out var fullPath))
        {
            return NotFound("File not found on disk");
        }

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

    private bool TryResolveStoredFilePath(
        string fileLink,
        out string relativePath,
        out string fullPath)
    {
        const string uploadPrefix = "/uploads/";

        relativePath = string.Empty;
        fullPath = string.Empty;

        if (!fileLink.StartsWith(uploadPrefix, StringComparison.Ordinal)
            || fileLink.Length == uploadPrefix.Length)
        {
            return false;
        }

        relativePath = fileLink[uploadPrefix.Length..];
        if (string.IsNullOrWhiteSpace(relativePath)
            || Path.IsPathRooted(relativePath))
        {
            relativePath = string.Empty;
            return false;
        }

        try
        {
            fullPath = _fileStorage.ResolveFullPath(relativePath);
            return true;
        }
        catch (InvalidOperationException)
        {
            relativePath = string.Empty;
            fullPath = string.Empty;
            return false;
        }
        catch (ArgumentException)
        {
            relativePath = string.Empty;
            fullPath = string.Empty;
            return false;
        }
        catch (NotSupportedException)
        {
            relativePath = string.Empty;
            fullPath = string.Empty;
            return false;
        }
        catch (IOException)
        {
            relativePath = string.Empty;
            fullPath = string.Empty;
            return false;
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

        var readiness = DocumentReadinessEvaluator.Evaluate(document);
        return Ok(new DocumentReadinessStatusResponseDto(
            document.Id,
            readiness.Status,
            readiness.IsChatReady,
            readiness.Message,
            readiness.CanRetry));
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
        if (request.File is null || request.File.Length <= 0)
        {
            return BadRequest(new
            {
                statusCode = StatusCodes.Status400BadRequest,
                message = "A non-empty file is required."
            });
        }

        await using var content = request.File.OpenReadStream();
        var result = await _uploadService.UploadAsync(
            new DocumentUploadRequest(
                GetCurrentUserId(),
                request.SubjectId,
                request.Title,
                request.File.FileName,
                request.File.ContentType,
                request.File.Length,
                content),
            cancellationToken);

        return Accepted(result);
    }

    [HttpPost("{id:guid}/reprocess")]
    public async Task<ActionResult<UploadDocumentResponseDto>> Reprocess(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await _uploadService.ReprocessAsync(
            id,
            GetCurrentUserId(),
            cancellationToken);
        return Accepted(result);
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
