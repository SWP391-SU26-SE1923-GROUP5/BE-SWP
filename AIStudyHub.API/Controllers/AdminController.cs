using AIStudyHub.Data.Interfaces;
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

    public AdminController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
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
