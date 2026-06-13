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
    private readonly IRagChatService _ragChatService;
    private readonly RagOptions _options;
    private readonly ILogger<DocumentUploadController> _logger;

    public DocumentUploadController(
        IUnitOfWork unitOfWork,
        IDocumentProcessingService documentProcessing,
        IEmbeddingService embeddingService,
        IVectorStoreService vectorStoreService,
        IFileStorageService fileStorage,
        IRagChatService ragChatService,
        IOptions<RagOptions> options,
        ILogger<DocumentUploadController> logger)
    {
        _unitOfWork = unitOfWork;
        _documentProcessing = documentProcessing;
        _embeddingService = embeddingService;
        _vectorStoreService = vectorStoreService;
        _fileStorage = fileStorage;
        _ragChatService = ragChatService;
        _options = options.Value;
        _logger = logger;
    }

    [HttpPost("upload")]
    public async Task<ActionResult<UploadDocumentResponseDto>> UploadDocument(
        [FromBody] UploadDocumentRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("Document title is required");

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        // Validate SubjectId exists
        var subject = await _unitOfWork.Subjects.GetByIdAsync(request.SubjectId, cancellationToken);
        if (subject == null)
            return BadRequest($"Subject with ID {request.SubjectId} not found");

        try
        {
            var fileContent = Convert.FromBase64String(request.FileBase64);

            // Validate extension
            if (!_fileStorage.IsValidExtension(request.FileExtension))
            {
                return BadRequest($"File extension '{request.FileExtension}' is not allowed. Allowed: .pdf, .docx, .txt, .md");
            }

            // Save file to disk
            var filePath = await _fileStorage.SaveFileAsync(fileContent, request.FileName, request.FileExtension, cancellationToken);
            var fileUrl = _fileStorage.GetFileUrl(filePath);

            var text = await _documentProcessing.ExtractTextAsync(fileContent, request.FileExtension);

            if (string.IsNullOrWhiteSpace(text))
            {
                // Clean up file if extraction fails
                await _fileStorage.DeleteFileAsync(filePath, cancellationToken);
                return BadRequest("Could not extract text from the document");
            }

            var chunks = await _documentProcessing.ChunkTextAsync(
                text,
                _options.ChunkSize,
                _options.ChunkOverlap);

            if (chunks.Count == 0)
            {
                await _fileStorage.DeleteFileAsync(filePath, cancellationToken);
                return BadRequest("No content chunks could be created from the document");
            }

            var document = new Document
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SubjectId = request.SubjectId,
                Title = request.Title,
                FileName = request.FileName,
                FileExtension = request.FileExtension,
                FileType = GetFileType(request.FileExtension),
                FileLink = fileUrl,
                ShareStatus = "private",
                Status = DocumentStatus.Published,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Documents.AddAsync(document);
            await _unitOfWork.SaveChangesAsync(cancellationToken); // Save Document first to get ID

            var embeddings = await _embeddingService.GenerateEmbeddingsAsync(chunks);

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

                await _unitOfWork.DocumentChunks.AddAsync(chunkEntity);

                var vectorId = await _vectorStoreService.UpsertVectorAsync(
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

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Document {DocumentId} uploaded with {ChunkCount} chunks by user {UserId}",
                document.Id, chunks.Count, userId);

            return Ok(new UploadDocumentResponseDto(
                document.Id,
                "completed",
                chunks.Count,
                $"Successfully processed {chunks.Count} chunks"
            ));
        }
        catch (FormatException)
        {
            return BadRequest("Invalid file content encoding");
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload document for user {UserId}", userId);
            return StatusCode(500, "An error occurred while processing the document");
        }
    }

    [HttpPost("upload/file")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(UploadDocumentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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

        try
        {
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

            var text = await _documentProcessing.ExtractTextAsync(fileContent, extension);

            if (string.IsNullOrWhiteSpace(text))
            {
                await _fileStorage.DeleteFileAsync(filePath, cancellationToken);
                return BadRequest("Could not extract text from the document");
            }

            var chunks = await _documentProcessing.ChunkTextAsync(
                text,
                _options.ChunkSize,
                _options.ChunkOverlap);

            if (chunks.Count == 0)
            {
                await _fileStorage.DeleteFileAsync(filePath, cancellationToken);
                return BadRequest("No content chunks could be created from the document");
            }

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
                ShareStatus = "private",
                Status = DocumentStatus.Published,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Documents.AddAsync(document);
            await _unitOfWork.SaveChangesAsync(cancellationToken); // Save Document first to get ID

            var embeddings = await _embeddingService.GenerateEmbeddingsAsync(chunks);

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

                await _unitOfWork.DocumentChunks.AddAsync(chunkEntity);

                await _vectorStoreService.UpsertVectorAsync(
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

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Document {DocumentId} uploaded (file) with {ChunkCount} chunks by user {UserId}",
                document.Id, chunks.Count, userId);

            return Ok(new UploadDocumentResponseDto(
                document.Id,
                "completed",
                chunks.Count,
                $"Successfully processed {chunks.Count} chunks"
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
                Math.Round(x.Score, 4)))
            .ToList();

        return Ok(result);
    }

    [HttpPost("{id:guid}/chat")]
    [SwaggerOperation(OperationId = "ChatWithDocument")]
    [ProducesResponseType(typeof(RagChatResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RagChatResponseDto>> ChatWithDocument(
        Guid id,
        [FromBody] RagChatRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var document = await _unitOfWork.Documents.GetByIdAsync(id);
        if (document == null)
            return NotFound("Document not found");

        var scopedRequest = new RagChatRequestDto(
            Message: request.Message,
            SessionId: request.SessionId,
            IncludeDocuments: true,
            DocumentIds: new List<Guid> { id });

        var result = await _ragChatService.ChatAsync(scopedRequest, userId);
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
