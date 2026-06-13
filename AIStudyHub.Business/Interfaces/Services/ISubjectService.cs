using AIStudyHub.Business.DTOs.Subjects;

namespace AIStudyHub.Business.Interfaces.Services;

public interface ISubjectService : ICrudService<SubjectResponseDto, CreateSubjectRequestDto, UpdateSubjectRequestDto>
{
}
