using AIStudyHub.Business.DTOs.Common;
using AIStudyHub.Business.DTOs.Subjects;

namespace AIStudyHub.Business.Interfaces.Services;

public interface ISubjectService
{
    Task<PagedResultDto<SubjectResponseDto>> GetMineAsync(Guid ownerUserId, PaginationParams pagination, CancellationToken cancellationToken = default);
    Task<SubjectResponseDto?> GetOwnedByIdAsync(Guid ownerUserId, Guid subjectId, CancellationToken cancellationToken = default);
    Task<SubjectResponseDto> CreateForUserAsync(Guid ownerUserId, CreateSubjectRequestDto request, CancellationToken cancellationToken = default);
    Task<SubjectResponseDto> UpdateOwnedAsync(Guid ownerUserId, Guid subjectId, UpdateSubjectRequestDto request, CancellationToken cancellationToken = default);
    Task DeleteOwnedAsync(Guid ownerUserId, Guid subjectId, CancellationToken cancellationToken = default);
    Task<bool> ExistsForOwnerAsync(Guid ownerUserId, Guid subjectId, CancellationToken cancellationToken = default);
}
