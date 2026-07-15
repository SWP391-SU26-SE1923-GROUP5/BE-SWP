using System.Text.Json;
using AIStudyHub.Business.Interfaces.AI.Generators;
using AIStudyHub.Business.Interfaces.AI.LLM;
using AIStudyHub.Business.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIStudyHub.Business.AI.Generators;

public sealed class DocumentSuggestedPromptService : IDocumentSuggestedPromptService
{
    private readonly IOpenAIService _openAiService;
    private readonly SuggestedPromptOptions _options;
    private readonly ILogger<DocumentSuggestedPromptService> _logger;

    public DocumentSuggestedPromptService(
        IOpenAIService openAiService,
        IOptions<SuggestedPromptOptions> options,
        ILogger<DocumentSuggestedPromptService> logger)
    {
        _openAiService = openAiService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> GenerateAsync(
        string documentText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentText))
            return [];

        try
        {
            var maxInputCharacters = Math.Max(1, _options.MaxInputCharacters);
            var content = documentText.Length > maxInputCharacters
                ? documentText[..maxInputCharacters]
                : documentText;
            var promptCount = Math.Max(1, _options.PromptCount);
            var prompt = $$"""
                Generate exactly {{promptCount}} useful questions that a student can ask and answer from the document below.
                Write every question in the same primary language as the document.
                Do not suggest unsupported actions, file exports, or capabilities.
                Return JSON only in this shape: {"prompts":["question 1","question 2","question 3"]}

                DOCUMENT:
                {{content}}
                """;

            var response = await _openAiService.SendMessageAsync(prompt);
            return ParsePrompts(response, promptCount, Math.Max(1, _options.MaxPromptLength));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate document suggested prompts");
            return [];
        }
    }

    private static IReadOnlyList<string> ParsePrompts(string? response, int count, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(response))
            return [];

        var json = response.Trim();
        if (json.StartsWith("```", StringComparison.Ordinal))
        {
            var firstLineEnd = json.IndexOf('\n');
            var lastFence = json.LastIndexOf("```", StringComparison.Ordinal);
            if (firstLineEnd >= 0 && lastFence > firstLineEnd)
                json = json[(firstLineEnd + 1)..lastFence].Trim();
        }

        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("prompts", out var prompts)
            || prompts.ValueKind != JsonValueKind.Array)
            return [];

        return prompts.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()?.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item) && item.Length <= maxLength)
            .Select(item => item!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(count)
            .ToList();
    }
}
