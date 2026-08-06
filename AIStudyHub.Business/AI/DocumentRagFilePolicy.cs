namespace AIStudyHub.Business.AI;

public static class DocumentRagFilePolicy
{
    private static readonly HashSet<string> TextExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".docx", ".txt", ".md"
        };

    private static readonly HashSet<string> ImageExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif"
        };

    public static bool IsTextDocument(string? fileName, string? fileExtension = null) =>
        TextExtensions.Contains(GetExtension(fileName, fileExtension));

    public static bool IsImageDocument(string? fileName, string? fileExtension = null) =>
        ImageExtensions.Contains(GetExtension(fileName, fileExtension));

    public static bool SupportsChat(string? fileName, string? fileExtension = null) =>
        IsTextDocument(fileName, fileExtension)
        || IsImageDocument(fileName, fileExtension);

    private static string GetExtension(string? fileName, string? fileExtension)
    {
        var extension = !string.IsNullOrWhiteSpace(fileExtension)
            ? fileExtension
            : Path.GetExtension(fileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(extension))
            return string.Empty;

        return extension.StartsWith('.')
            ? extension.ToLowerInvariant()
            : $".{extension.ToLowerInvariant()}";
    }
}
