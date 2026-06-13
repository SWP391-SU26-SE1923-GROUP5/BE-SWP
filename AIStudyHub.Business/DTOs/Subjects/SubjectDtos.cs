namespace AIStudyHub.Business.DTOs.Subjects;

public sealed record SubjectResponseDto(Guid Id, string SubjectCode, string SubjectName, string? Description, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record CreateSubjectRequestDto(string SubjectCode, string SubjectName, string? Description);

public sealed record UpdateSubjectRequestDto(string SubjectCode, string SubjectName, string? Description);
