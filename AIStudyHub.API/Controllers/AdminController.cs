using AIStudyHub.Business.Services;
using AIStudyHub.Data.Interfaces;
using AIStudyHub.Business.DTOs.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDocumentProcessingQueue _queue;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IUnitOfWork unitOfWork,
        IDocumentProcessingQueue queue,
        ILogger<AdminController> logger)
    {
        _unitOfWork = unitOfWork;
        _queue = queue;
        _logger = logger;
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
}
