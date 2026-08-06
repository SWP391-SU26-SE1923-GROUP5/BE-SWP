namespace AIStudyHub.Business.Options;

public sealed class DocumentStorageOptions
{
    public string BasePath { get; set; } = "uploads/documents";
    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;
    public string[] AllowedExtensions { get; set; } = [".pdf", ".docx", ".txt", ".md", ".jpg", ".jpeg", ".png", ".gif", ".mp4", ".avi", ".mov", ".webm", ".mp3", ".wav", ".ogg", ".m4a"];
}
