namespace AIStudyHub.Business.Interfaces.AI.Generators;

public interface IDocumentSuggestedPromptService
{
    Task<IReadOnlyList<string>> GenerateAsync(
        string documentText,
        CancellationToken cancellationToken = default);
}
