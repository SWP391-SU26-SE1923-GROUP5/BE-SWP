namespace AIStudyHub.Data.Entities;

/// <summary>
/// Represents an academic subject used to categorize learning documents.
/// </summary>
public sealed class Subject : BaseEntity
{
    /// <summary>
    /// Gets or sets the unique subject code.
    /// </summary>
    public string SubjectCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the subject name.
    /// </summary>
    public string SubjectName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the subject description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the documents associated with the subject.
    /// </summary>
    public ICollection<Document> Documents { get; set; } = [];
}
