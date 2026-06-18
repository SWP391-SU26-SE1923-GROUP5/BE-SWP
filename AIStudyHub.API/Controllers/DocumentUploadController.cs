using AIStudyHub.API.DTOs;
using AIStudyHub.API.Swagger;
using AIStudyHub.Business.DTOs.Rag;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Options;
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

    private readonly IServiceScopeFactory _serviceScopeFactory;

    public DocumentUploadController(
        IUnitOfWork unitOfWork,
        IDocumentProcessingService documentProcessing,
        IEmbeddingService embeddingService,
        IVectorStoreService vectorStoreService,
        IFileStorageService fileStorage,
        IOptions<RagOptions> options,
        ILogger<DocumentUploadController> logger,
        IServiceScopeFactory serviceScopeFactory)
    {
        _unitOfWork = unitOfWork;
        _documentProcessing = documentProcessing;
        _embeddingService = embeddingService;
        _vectorStoreService = vectorStoreService;
        _fileStorage = fileStorage;
        _options = options.Value;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
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

            // Run heavy extraction and embedding in background
            _ = Task.Run(async () =>
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var scopedUnitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var scopedDocProcessing = scope.ServiceProvider.GetRequiredService<IDocumentProcessingService>();
                var scopedEmbedding = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
                var scopedVectorStore = scope.ServiceProvider.GetRequiredService<IVectorStoreService>();
                var scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<DocumentUploadController>>();
                var scopedOptions = scope.ServiceProvider.GetRequiredService<IOptions<RagOptions>>().Value;

                try
                {
                    var text = await scopedDocProcessing.ExtractTextAsync(fileContent, extension);
                    if (string.IsNullOrWhiteSpace(text)) throw new Exception("Could not extract text from the document");

                    var chunks = await scopedDocProcessing.ChunkTextAsync(text, scopedOptions.ChunkSize, scopedOptions.ChunkOverlap);
                    if (chunks.Count == 0) throw new Exception("No content chunks could be created");

                    var embeddings = await scopedEmbedding.GenerateEmbeddingsAsync(chunks);

                    for (var i = 0; i < chunks.Count; i++)
                    {
                        var chunk = chunks[i];
                        var embedding = embeddings[i];
                        var chunkEntity = new DocumentChunk
                        {
                            Id = Guid.NewGuid(),
                            DocumentId = document.Id,
                            ChunkJson = chunk,
                            EmbeddingJson = System.Text.Json.JsonSerializer.Serialize(embedding),
                            Vector = ConvertToByteArray(embedding),
                            OrderIndex = i,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        await scopedUnitOfWork.DocumentChunks.AddAsync(chunkEntity);

                        await scopedVectorStore.UpsertVectorAsync(
                            chunkEntity.Id.ToString(),
                            embedding,
                            new Dictionary<string, string>
                            {
                                ["documentId"] = document.Id.ToString(),
                                ["userId"] = userId.ToString(),
                                ["chunkIndex"] = i.ToString(),
                                ["documentTitle"] = document.Title
                            });
                    }

                    var docToUpdate = await scopedUnitOfWork.Documents.GetByIdAsync(document.Id);
                    if (docToUpdate != null)
                    {
                        docToUpdate.Status = DocumentStatus.Published;
                        scopedUnitOfWork.Documents.Update(docToUpdate);
                    }
                    await scopedUnitOfWork.SaveChangesAsync(CancellationToken.None);
                    scopedLogger.LogInformation("Background processing completed for Document {DocumentId} with {ChunkCount} chunks", document.Id, chunks.Count);
                }
                catch (Exception ex)
                {
                    scopedLogger.LogError(ex, "Background processing failed for Document {DocumentId}", document.Id);
                    var docToUpdate = await scopedUnitOfWork.Documents.GetByIdAsync(document.Id);
                    if (docToUpdate != null)
                    {
                        docToUpdate.Status = DocumentStatus.Failed;
                        scopedUnitOfWork.Documents.Update(docToUpdate);
                        await scopedUnitOfWork.SaveChangesAsync(CancellationToken.None);
                    }
                }
            });

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

        var chunks = await _unitOfWork.DocumentChunks
            .Query()
            .Where(c => c.DocumentId == id)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(chunks.Select(c => new ChunkDto(
            c.Id,
            c.DocumentId,
            c.ChunkJson ?? "",
            c.OrderIndex,
            null
        )).ToList());
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
            var chunks = await _unitOfWork.DocumentChunks
                .Query()
                .Where(c => c.DocumentId == id)
                .ToListAsync(cancellationToken);

            foreach (var chunk in chunks)
            {
                if (!string.IsNullOrEmpty(chunk.VectorId))
                {
                    await _vectorStoreService.DeleteVectorAsync(chunk.VectorId);
                }
                _unitOfWork.DocumentChunks.Remove(chunk);
            }

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

    [HttpGet("{id:guid}/chunks/search")]
    [SwaggerOperation(OperationId = "SearchDocumentChunks")]
    public async Task<ActionResult<List<ChunkDto>>> SearchDocumentChunks(
        Guid id,
        [FromQuery] string q,
        [FromQuery] int top = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest("Query parameter 'q' is required");

        var document = await _unitOfWork.Documents.GetByIdAsync(id);
        if (document == null)
            return NotFound("Document not found");

        var chunks = await _unitOfWork.DocumentChunks
            .Query()
            .Where(c => c.DocumentId == id)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (chunks.Count == 0)
            return Ok(Enumerable.Empty<ChunkDto>());

        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(q);

        var scored = chunks
            .Select(c =>
            {
                var embedding = DeserializeEmbedding(c.EmbeddingJson);
                var score = embedding != null ? CosineSimilarity(queryEmbedding, embedding) : 0f;
                return (Chunk: c, Score: score);
            })
            .OrderByDescending(x => x.Score)
            .Take(top)
            .ToList();

        var result = scored
            .Select(x => new ChunkDto(
                x.Chunk.Id,
                x.Chunk.DocumentId,
                x.Chunk.ChunkJson ?? "",
                x.Chunk.OrderIndex,
                null))
            .ToList();

        return Ok(result);
    }

    private static float[]? DeserializeEmbedding(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement
                .EnumerateArray()
                .Select(e => e.GetSingle())
                .ToArray();
        }
        catch
        {
            return null;
        }
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0)
            return 0;

        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denominator = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denominator == 0 ? 0 : (float)(dot / denominator);
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

    private static byte[] ConvertToByteArray(float[] floats)
    {
        var bytes = new byte[floats.Length * sizeof(float)];
        Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}
