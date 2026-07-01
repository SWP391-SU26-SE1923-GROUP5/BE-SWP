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
