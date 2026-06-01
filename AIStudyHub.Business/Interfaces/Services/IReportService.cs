using AIStudyHub.Business.DTOs.Reports;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IReportService : ICrudService<ReportResponseDto, CreateReportRequestDto, UpdateReportRequestDto>
{
}
