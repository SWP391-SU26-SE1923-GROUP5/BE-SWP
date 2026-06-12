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
    private readonly RagOptions _options;
    private readonly ILogger<DocumentUploadController> _logger;

    public DocumentUploadController(
        IUnitOfWork unitOfWork,
        IDocumentProcessingService documentProcessing,
        IEmbeddingService embeddingService,
        IVectorStoreService vectorStoreService,
        IOptions<RagOptions> options,
        ILogger<DocumentUploadController> logger)
    {
        _unitOfWork = unitOfWork;
        _documentProcessing = documentProcessing;
        _embeddingService = embeddingService;
        _vectorStoreService = vectorStoreService;
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

        try
        {
            var fileContent = Convert.FromBase64String(request.FileBase64);
            var text = await _documentProcessing.ExtractTextAsync(fileContent, request.FileExtension);

            if (string.IsNullOrWhiteSpace(text))
                return BadRequest("Could not extract text from the document");

            var chunks = await _documentProcessing.ChunkTextAsync(
                text,
                _options.ChunkSize,
                _options.ChunkOverlap);

            if (chunks.Count == 0)
                return BadRequest("No content chunks could be created from the document");

            var document = new Document
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SubjectId = request.SubjectId,
                Title = request.Title,
                FileName = request.FileName,
                FileExtension = request.FileExtension,
                FileType = GetFileType(request.FileExtension),
                ShareStatus = "private",
                Status = DocumentStatus.Published,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Documents.AddAsync(document);

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
            0,
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
