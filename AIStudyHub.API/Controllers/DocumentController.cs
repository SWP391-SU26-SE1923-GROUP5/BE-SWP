using AIStudyHub.Business.DTOs.Documents;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class DocumentController : ControllerBase
{
    private readonly IDocumentService _service;

    public DocumentController(IDocumentService service)
    {
        _service = service;
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
}
