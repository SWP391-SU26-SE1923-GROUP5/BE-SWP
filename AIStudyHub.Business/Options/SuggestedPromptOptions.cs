namespace AIStudyHub.Business.Options;

public sealed class SuggestedPromptOptions
{
    public const string SectionName = "SuggestedPrompts";

    public int PromptCount { get; set; } = 3;
    public int MaxInputCharacters { get; set; } = 4_000;
    public int MaxPromptLength { get; set; } = 160;
}
