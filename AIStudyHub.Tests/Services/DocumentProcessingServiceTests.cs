using System;
using System.Text;
using System.Threading.Tasks;
using AIStudyHub.Business.Services;
using AIStudyHub.Business.DTOs.Documents;
using AIStudyHub.Business.Enums;
using Xunit;

namespace AIStudyHub.Tests.Services;

public class DocumentProcessingServiceTests
{
    private readonly DocumentProcessingService _service;

    public DocumentProcessingServiceTests()
    {
        _service = new DocumentProcessingService();
    }

    [Fact]
    public async Task ExtractTextAsync_UnsupportedExtension_ThrowsNotSupportedException()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes("test");

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(() => _service.ExtractTextAsync(content, ".xyz"));
    }

    [Fact]
    public async Task ExtractTextAsync_TxtFile_ReturnsText()
    {
        // Arrange
        var text = "Hello World! This is a test.";
        var content = Encoding.UTF8.GetBytes(text);

        // Act
        var result = await _service.ExtractTextAsync(content, ".txt");

        // Assert
        Assert.Equal(text, result);
    }

    [Fact]
    public async Task ExtractTextAsync_InvalidPdf_ReturnsFailedMessage()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes("Not a real PDF");

        // Act
        var result = await _service.ExtractTextAsync(content, ".pdf");

        // Assert
        Assert.Contains("[PDF extraction failed:", result);
    }

    [Fact]
    public async Task ExtractTextAsync_InvalidDocx_ReturnsFailedMessage()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes("Not a real DOCX");

        // Act
        var result = await _service.ExtractTextAsync(content, ".docx");

        // Assert
        Assert.Contains("[DOCX extraction failed:", result);
    }

    [Fact]
    public async Task ExtractSegmentsAsync_TxtFile_ReturnsHighlightableVerbatimSegment()
    {
        var content = Encoding.UTF8.GetBytes("Visible source text.");

        var segments = await _service.ExtractSegmentsAsync(content, ".txt");

        var segment = Assert.Single(segments);
        Assert.Equal("Visible source text.", segment.Text);
        Assert.Equal(DocumentContentType.Verbatim, segment.ContentType);
        Assert.True(segment.IsHighlightable);
        Assert.Null(segment.PageNumber);
    }

    [Fact]
    public async Task ExtractSegmentsAsync_InvalidPdf_DoesNotReturnErrorAsContent()
    {
        var content = Encoding.UTF8.GetBytes("Not a real PDF");

        var segments = await _service.ExtractSegmentsAsync(content, ".pdf");

        Assert.Empty(segments);
    }

    [Fact]
    public async Task ChunkTextAsync_SplitsTextCorrectly()
    {
        // Arrange
        var text = "This is sentence one. This is sentence two! And sentence three? Finally, four.";
        // chunkSize of 30 characters
        
        // Act
        var chunks = await _service.ChunkTextAsync(text, chunkSize: 30, overlap: 0);

        // Assert
        // Expected grouping based on size and sentences.
        Assert.NotEmpty(chunks);
        Assert.True(chunks.Count > 1);
        Assert.Contains("This is sentence one.", chunks[0].Text);
    }

    [Fact]
    public async Task ChunkTextAsync_HandlesOverlap()
    {
        // Arrange
        var text = "First sentence. Second sentence. Third sentence.";
        
        // Act
        var chunks = await _service.ChunkTextAsync(text, chunkSize: 20, overlap: 5);

        // Assert
        Assert.True(chunks.Count > 1);
        // Ensure overlap is included in subsequent chunks (part of previous chunk)
        // With 5 char overlap, the last 5 chars of chunk[0] should be at the start of chunk[1]
        var chunk0 = chunks[0].Text;
        var chunk1 = chunks[1].Text;
        
        var overlapText = chunk0.Substring(chunk0.Length - 5);
        Assert.StartsWith(overlapText, chunk1);
    }

    [Fact]
    public async Task ChunkSegmentsAsync_DoesNotCrossPageBoundaries()
    {
        ExtractedTextSegment[] segments =
        [
            new("End of page one.", DocumentContentType.Verbatim, 1, true),
            new("Start of page two.", DocumentContentType.Verbatim, 2, true)
        ];

        var chunks = await _service.ChunkSegmentsAsync(segments, 200, 20);

        Assert.Collection(chunks,
            first =>
            {
                Assert.Equal(1, first.PageNumber);
                Assert.Equal("End of page one.", first.Text);
            },
            second =>
            {
                Assert.Equal(2, second.PageNumber);
                Assert.Equal("Start of page two.", second.Text);
            });
    }

    [Fact]
    public async Task ChunkSegmentsAsync_DoesNotMixContentTypes()
    {
        ExtractedTextSegment[] segments =
        [
            new("Visible paragraph.", DocumentContentType.Verbatim, null, true),
            new("Generated overview.", DocumentContentType.Summary, null, false)
        ];

        var chunks = await _service.ChunkSegmentsAsync(segments, 200, 0);

        Assert.Collection(chunks,
            first => Assert.Equal(DocumentContentType.Verbatim, first.ContentType),
            second => Assert.Equal(DocumentContentType.Summary, second.ContentType));
        Assert.True(chunks[0].IsHighlightable);
        Assert.False(chunks[1].IsHighlightable);
    }

    [Fact]
    public async Task ChunkSegmentsAsync_RemovesTechnicalArtifactsButPreservesSourceStructure()
    {
        ExtractedTextSegment[] segments =
        [
            new("""
                [--- Page 1 ---]
                Keep https://example.com/reference
                file:///C:/print/source.html
                [OCR image extraction failed: missing language data]
                | Name | Value |
                | --- | --- |
                | A | 1 |
                """, DocumentContentType.Verbatim, 1, true),
            new("[PDF extraction failed: corrupt]", DocumentContentType.SystemError, null, false)
        ];

        var chunks = await _service.ChunkSegmentsAsync(segments, 500, 0);

        var chunk = Assert.Single(chunks);
        Assert.DoesNotContain("[--- Page", chunk.Text);
        Assert.DoesNotContain("file:///", chunk.Text);
        Assert.DoesNotContain("extraction failed", chunk.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("https://example.com/reference", chunk.Text);
        Assert.Contains("| Name | Value |\n| --- | --- |\n| A | 1 |", chunk.Text);
    }

    [Fact]
    public async Task ChunkSegmentsAsync_RemovesPdfPrintedPageFooterPrefix()
    {
        ExtractedTextSegment[] segments =
        [
            new("8 | Page     3. Business Rules BR-01 Free tier limit.",
                DocumentContentType.Verbatim, 8, true)
        ];

        var chunks = await _service.ChunkSegmentsAsync(segments, 500, 0);

        var chunk = Assert.Single(chunks);
        Assert.StartsWith("3. Business Rules", chunk.Text);
        Assert.DoesNotContain("8 | Page", chunk.Text);
        Assert.Equal(8, chunk.PageNumber);
    }
}
