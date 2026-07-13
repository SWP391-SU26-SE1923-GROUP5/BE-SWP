using System;
using System.Text;
using System.Threading.Tasks;
using AIStudyHub.Business.Services;
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
}
