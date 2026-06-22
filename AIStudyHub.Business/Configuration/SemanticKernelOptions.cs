namespace AIStudyHub.Business.Configuration;

public class SemanticKernelOptions
{
    public GenerationOptions Generation { get; set; } = new();
    public PromptOptions Prompts { get; set; } = new();
}

public class GenerationOptions
{
    public int MaxTokens { get; set; } = 2048;
    public double Temperature { get; set; } = 0.3;
}

public class PromptOptions
{
    public string RagSystemPrompt { get; set; } = """
        You are a helpful AI assistant. Answer questions based only on the provided context.
        If you cannot find the answer in the context, say "I cannot find this information in the provided documents."
        Always cite your sources using [Source1], [Source2], etc.
        """;

    public string RagUserPromptTemplate { get; set; } = """
        Context:
        {{$context}}
        
        Question: {{$question}}
        
        Answer:
        """;
}
