using AIStudyHub.Business.AI.Generators;
using AIStudyHub.Business.Interfaces.AI.LLM;
using AIStudyHub.Business.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AIStudyHub.Tests.Services;

public sealed class DocumentSuggestedPromptServiceTests
{
    [Fact]
    public async Task GenerateAsync_ReturnsThreeUniqueValidatedPrompts()
    {
        var openAi = new Mock<IOpenAIService>();
        openAi.Setup(service => service.SendMessageAsync(It.IsAny<string>()))
            .ReturnsAsync("""
                ```json
                {"prompts":["Chủ đề chính là gì?","Chủ đề chính là gì?","Các kết luận quan trọng là gì?","Những khái niệm nào cần ghi nhớ?"]}
                ```
                """);
        var service = new DocumentSuggestedPromptService(
            openAi.Object,
            Options.Create(new SuggestedPromptOptions
            {
                PromptCount = 3,
                MaxInputCharacters = 2_000,
                MaxPromptLength = 120
            }),
            Mock.Of<ILogger<DocumentSuggestedPromptService>>());

        var prompts = await service.GenerateAsync(
            "Đây là nội dung tài liệu tiếng Việt về kiến trúc phần mềm.",
            CancellationToken.None);

        Assert.Equal(3, prompts.Count);
        Assert.Equal("Chủ đề chính là gì?", prompts[0]);
        Assert.Equal("Các kết luận quan trọng là gì?", prompts[1]);
        Assert.Equal("Những khái niệm nào cần ghi nhớ?", prompts[2]);
        openAi.Verify(service => service.SendMessageAsync(
            It.Is<string>(prompt => prompt.Contains("same primary language as the document"))), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_LlmFailure_ReturnsEmptyPrompts()
    {
        var openAi = new Mock<IOpenAIService>();
        openAi.Setup(service => service.SendMessageAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("LLM unavailable"));
        var service = new DocumentSuggestedPromptService(
            openAi.Object,
            Options.Create(new SuggestedPromptOptions()),
            Mock.Of<ILogger<DocumentSuggestedPromptService>>());

        var prompts = await service.GenerateAsync("Document content", CancellationToken.None);

        Assert.Empty(prompts);
    }
}
