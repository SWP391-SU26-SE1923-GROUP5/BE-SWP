using System;
using System.IO;
using System.Threading.Tasks;
using AIStudyHub.Business.Options;
using AIStudyHub.Business.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace AIStudyHub.Tests.Services;

public class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _testBasePath;
    private readonly Mock<ILogger<LocalFileStorageService>> _loggerMock;
    private readonly IOptions<DocumentStorageOptions> _options;
    private readonly LocalFileStorageService _service;

    public LocalFileStorageServiceTests()
    {
        _testBasePath = Path.Combine(Path.GetTempPath(), "AIStudyHub_Tests", Guid.NewGuid().ToString());
        
        var options = new DocumentStorageOptions
        {
            BasePath = _testBasePath,
            AllowedExtensions = new[] { ".txt", ".pdf" }
        };
        _options = Microsoft.Extensions.Options.Options.Create(options);
        
        _loggerMock = new Mock<ILogger<LocalFileStorageService>>();
        _service = new LocalFileStorageService(_options, _loggerMock.Object);
    }

    [Fact]
    public void IsValidExtension_ValidExtension_ReturnsTrue()
    {
        Assert.True(_service.IsValidExtension(".txt"));
        Assert.True(_service.IsValidExtension("pdf"));
    }

    [Fact]
    public void IsValidExtension_InvalidExtension_ReturnsFalse()
    {
        Assert.False(_service.IsValidExtension(".exe"));
    }

    [Fact]
    public async Task SaveFileAsync_CreatesFileAndReturnsRelativePath()
    {
        // Arrange
        var content = new byte[] { 1, 2, 3 };

        // Act
        var relativePath = await _service.SaveFileAsync(content, "test", ".txt");

        // Assert
        Assert.NotNull(relativePath);
        Assert.Contains("test.txt", relativePath);

        var fullPath = Path.Combine(_testBasePath, relativePath);
        Assert.True(File.Exists(fullPath));
    }

    [Fact]
    public async Task DeleteFileAsync_ExistingFile_DeletesFile()
    {
        // Arrange
        var content = new byte[] { 1, 2, 3 };
        var relativePath = await _service.SaveFileAsync(content, "deleteMe", ".txt");
        var fullPath = Path.Combine(_testBasePath, relativePath);

        // Act
        await _service.DeleteFileAsync(relativePath);

        // Assert
        Assert.False(File.Exists(fullPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testBasePath))
        {
            Directory.Delete(_testBasePath, true);
        }
    }
}
