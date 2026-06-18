using AIStudyHub.Business.DTOs.Documents;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class DocumentController : ControllerBase
{
    private readonly IDocumentService _service;
    private readonly DocumentStorageOptions _storageOptions;

    public DocumentController(IDocumentService service, IOptions<DocumentStorageOptions> storageOptions)
    {
        _service = service;
        _storageOptions = storageOptions.Value;
    }

    /// <summary>Lấy danh sách tất cả tài liệu.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DocumentResponseDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>Lấy thông tin một tài liệu theo ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DocumentResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    // POST   /api/Document - Đã xóa. Dùng POST /api/DocumentUpload/upload/file để upload và tạo Document (có AI pipeline).

    /// <summary>
    /// Cập nhật metadata tài liệu (title, shareStatus...).
    /// Lưu ý: Endpoint này CHỈ cập nhật metadata trong DB.
    /// File vật lý và embedding vector KHÔNG thay đổi.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DocumentResponseDto>> Update(Guid id, [FromBody] UpdateDocumentRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _service.UpdateAsync(id, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Xóa metadata tài liệu khỏi DB.
    /// Lưu ý: Để xóa toàn bộ (file vật lý + chunks + vectors), dùng DELETE /api/DocumentUpload/{id}.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/download")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var document = await _service.GetByIdAsync(id, cancellationToken);
        if (document is null)
            return NotFound();

        if (string.IsNullOrEmpty(document.FileLink))
            return NotFound("No file associated with this document");

        var relativePath = document.FileLink.Replace("/uploads/", "");
        var fullPath = Path.Combine(_storageOptions.BasePath, relativePath);

        if (!System.IO.File.Exists(fullPath))
            return NotFound("File not found on disk");

        var contentType = document.FileType ?? "application/octet-stream";
        var fileName = document.FileName ?? Path.GetFileName(relativePath);

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return File(stream, contentType, fileName);
    }
}
