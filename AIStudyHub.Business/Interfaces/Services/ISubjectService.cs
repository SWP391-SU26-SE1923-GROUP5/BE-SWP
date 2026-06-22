using AIStudyHub.Business.DTOs.Subjects;

namespace AIStudyHub.Business.Interfaces.Services;

public interface ISubjectService : ICrudService<SubjectResponseDto, CreateSubjectRequestDto, UpdateSubjectRequestDto>
{
    Task<AIStudyHub.Business.DTOs.Common.PagedResultDto<SubjectResponseDto>> GetAllPagedAsync(AIStudyHub.Business.DTOs.Common.PaginationParams @params, CancellationToken cancellationToken = default);
}
