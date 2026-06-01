using AIStudyHub.Business.DTOs.Reports;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[Route("api/[controller]")]
public sealed class ReportController : CrudControllerBase<ReportResponseDto, CreateReportRequestDto, UpdateReportRequestDto>
{
    public ReportController(IReportService service)
        : base(service)
    {
    }
}
