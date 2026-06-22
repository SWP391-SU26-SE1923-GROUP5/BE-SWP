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

    /// <summary>Lấy danh sách tất cả tài liệu (có hỗ trợ tìm kiếm và lọc theo môn học).</summary>
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

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub")
            ?? User.FindFirst("userId");

        return claim != null && Guid.TryParse(claim.Value, out var userId)
            ? userId
            : Guid.Empty;
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
    /// Lưu danh sách người dùng được chia sẻ tài liệu và cập nhật trạng thái chia sẻ.
    /// Chỉ chủ sở hữu tài liệu mới có thể thay đổi quyền chia sẻ.
    /// </summary>
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

    [HttpGet("{id:guid}/preview")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Preview(Guid id, CancellationToken cancellationToken)
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
        Response.Headers.Append("Content-Disposition", $"inline; filename=\"{fileName}\"");
        return File(stream, contentType);
    }
}
