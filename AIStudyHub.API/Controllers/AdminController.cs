using AIStudyHub.Data.Interfaces;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/[controller]")]
public sealed class AdminController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDocumentService _documentService;

    public AdminController(IUnitOfWork unitOfWork, IDocumentService documentService)
    {
        _unitOfWork = unitOfWork;
        _documentService = documentService;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<AdminDashboardDto>> GetDashboard(CancellationToken cancellationToken)
    {
        var totalUsers = await _unitOfWork.Users.Query().CountAsync(cancellationToken);
        var totalDocuments = await _unitOfWork.Documents.Query().CountAsync(cancellationToken);
        var totalPayments = await _unitOfWork.Payments.Query().CountAsync(cancellationToken);
        var pendingPayments = await _unitOfWork.Payments.Query().CountAsync(p => p.Status == Data.Enums.PaymentStatus.Pending, cancellationToken);
        var completedPayments = await _unitOfWork.Payments.Query().CountAsync(p => p.Status == Data.Enums.PaymentStatus.Completed, cancellationToken);
        var totalReports = await _unitOfWork.Reports.Query().CountAsync(cancellationToken);
        var totalFlashcards = await _unitOfWork.Flashcards.Query().CountAsync(cancellationToken);
        var totalQuizzes = await _unitOfWork.Quizzes.Query().CountAsync(cancellationToken);

        return Ok(new AdminDashboardDto(
            totalUsers,
            totalDocuments,
            totalPayments,
            pendingPayments,
            completedPayments,
            totalReports,
            totalFlashcards,
            totalQuizzes,
            DateTime.UtcNow));
    }

    [HttpGet("documents")]
    public async Task<ActionResult<IReadOnlyList<AIStudyHub.Business.DTOs.Documents.DocumentResponseDto>>> GetAllDocuments(CancellationToken cancellationToken)
    {
        var result = await _documentService.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPut("documents/{id:guid}/ban")]
    public async Task<IActionResult> BanDocument(Guid id, CancellationToken cancellationToken)
    {
        var document = await _unitOfWork.Documents.GetByIdAsync(id, cancellationToken);
        if (document is null) return NotFound(new { message = "Document not found" });

        document.Status = Data.Enums.DocumentStatus.Banned;
        document.ShareStatus = "private"; // Force the document to be private
        document.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Documents.Update(document);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Document has been banned successfully." });
    }
}

public sealed record AdminDashboardDto(
    int TotalUsers,
    int TotalDocuments,
    int TotalPayments,
    int PendingPayments,
    int CompletedPayments,
    int TotalReports,
    int TotalFlashcards,
    int TotalQuizzes,
    DateTime GeneratedAt);
