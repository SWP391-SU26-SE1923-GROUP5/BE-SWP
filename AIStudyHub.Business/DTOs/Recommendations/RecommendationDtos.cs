namespace AIStudyHub.Business.DTOs.Recommendations;

public sealed record SubjectMasteryDto(
    string SubjectCode,
    string SubjectName,
    double MasteryPercentage,
    int TotalAttempts,
    int CorrectAttempts);

public sealed record RecommendationResultDto(
    IReadOnlyList<SubjectMasteryDto> SubjectMasteries,
    IReadOnlyList<string> Recommendations,
    string? Summary);

public sealed record RecommendationResponseDto(
    Guid Id,
    Guid UserId,
    string Type,
    Guid? ReferenceId,
    string Title,
    string Description,
    string? ActionUrl,
    string Status,
    DateTime? DismissedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
