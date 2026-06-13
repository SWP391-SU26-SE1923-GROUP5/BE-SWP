namespace AIStudyHub.Business.Options;

public sealed class DocumentStorageOptions
{
    public string BasePath { get; set; } = "uploads/documents";
    public long MaxFileSizeBytes { get; set; } = 50 * 1024 * 1024; // 50MB default
    public string[] AllowedExtensions { get; set; } = [".pdf", ".docx", ".txt", ".md"];
}
