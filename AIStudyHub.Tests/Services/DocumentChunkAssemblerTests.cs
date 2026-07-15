using AIStudyHub.Business.DTOs.Documents;
using AIStudyHub.Business.Enums;
using AIStudyHub.Business.Services;

namespace AIStudyHub.Tests.Services;

public sealed class DocumentChunkAssemblerTests
{
    [Fact]
    public async Task AssembleAsync_PrependsNonHighlightableSummaryAndPreservesSourceMetadata()
    {
        var processor = new DocumentProcessingService();
        ExtractedTextSegment[] segments =
        [
            new("Page source text.", DocumentContentType.Verbatim, 3, true)
        ];

        var chunks = await DocumentChunkAssembler.AssembleAsync(
            processor, segments, "Generated summary.", 500, 0);

        Assert.Collection(chunks,
            summary =>
            {
                Assert.Equal(DocumentContentType.Summary, summary.ContentType);
                Assert.False(summary.IsHighlightable);
                Assert.Null(summary.PageNumber);
                Assert.Equal("Generated summary.", summary.Text);
            },
            source =>
            {
                Assert.Equal(DocumentContentType.Verbatim, source.ContentType);
                Assert.True(source.IsHighlightable);
                Assert.Equal(3, source.PageNumber);
            });
    }
}
