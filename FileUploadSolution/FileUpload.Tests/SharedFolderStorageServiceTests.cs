using FileUpload.Api.Exceptions;
using FileUpload.Api.Options;
using FileUpload.Api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FileUpload.Tests;

public class SharedFolderStorageServiceTests
{
    private readonly Mock<ILogger<SharedFolderStorageService>> _mockLogger;

    public SharedFolderStorageServiceTests()
    {
        _mockLogger = new Mock<ILogger<SharedFolderStorageService>>();
    }

    [Fact]
    public async Task SaveFileAsync_SuccessfullySavesFile_ToTempDirectory()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempFolder);

        try
        {
            var options = Options.Create(new FileStorageOptions
            {
                SharedFolderPath = tempFolder,
                MaxFileSizeInMB = 10,
                AllowedExtensions = new[] { ".pdf", ".txt" }
            });

            var service = new SharedFolderStorageService(options, _mockLogger.Object);
            var sampleContent = "Hello World PDF Content"u8.ToArray();
            using var stream = new MemoryStream(sampleContent);

            var response = await service.SaveFileAsync(stream, "test.pdf", "application/pdf");

            Assert.NotNull(response);
            Assert.False(string.IsNullOrEmpty(response.FileId));
            Assert.EndsWith(".pdf", response.FileName);
            Assert.Equal(sampleContent.Length, response.FileSizeInBytes);

            var savedFilePath = Path.Combine(tempFolder, response.FileName);
            Assert.True(File.Exists(savedFilePath));
        }
        finally
        {
            if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);
        }
    }

    [Fact]
    public async Task SaveFileAsync_ThrowsFileStorageException_WhenDirectoryDoesNotExist()
    {
        var options = Options.Create(new FileStorageOptions
        {
            SharedFolderPath = @"Z:\NonExistentNetworkSharePath_12345",
            MaxFileSizeInMB = 10,
            AllowedExtensions = new[] { ".pdf" }
        });

        var service = new SharedFolderStorageService(options, _mockLogger.Object);
        using var stream = new MemoryStream("data"u8.ToArray());

        var ex = await Assert.ThrowsAsync<FileStorageException>(() =>
            service.SaveFileAsync(stream, "test.pdf", "application/pdf"));

        Assert.Equal(503, ex.StatusCode);
    }

    [Fact]
    public async Task SaveFileAsync_ThrowsFileStorageException_WhenExtensionDisallowed()
    {
        var tempFolder = Path.GetTempPath();
        var options = Options.Create(new FileStorageOptions
        {
            SharedFolderPath = tempFolder,
            MaxFileSizeInMB = 10,
            AllowedExtensions = new[] { ".pdf" }
        });

        var service = new SharedFolderStorageService(options, _mockLogger.Object);
        using var stream = new MemoryStream("data"u8.ToArray());

        var ex = await Assert.ThrowsAsync<FileStorageException>(() =>
            service.SaveFileAsync(stream, "malicious.exe", "application/octet-stream"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("not allowed", ex.Message);
    }
}
