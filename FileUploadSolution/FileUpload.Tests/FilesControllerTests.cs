using FileUpload.Api.Controllers;
using FileUpload.Api.Models;
using FileUpload.Api.Options;
using FileUpload.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FileUpload.Tests;

public class FilesControllerTests
{
    private readonly Mock<IFileStorageService> _mockStorageService;
    private readonly Mock<ILogger<FilesController>> _mockLogger;
    private readonly IOptions<FileStorageOptions> _options;

    public FilesControllerTests()
    {
        _mockStorageService = new Mock<IFileStorageService>();
        _mockLogger = new Mock<ILogger<FilesController>>();

        _options = Options.Create(new FileStorageOptions
        {
            SharedFolderPath = Path.GetTempPath(),
            MaxFileSizeInMB = 10,
            AllowedExtensions = new[] { ".pdf", ".docx", ".png" }
        });
    }

    [Fact]
    public async Task UploadFile_ReturnsBadRequest_WhenFileIsNull()
    {
        var controller = new FilesController(_mockStorageService.Object, _options, _mockLogger.Object);
        SetupHttpContext(controller);

        var result = await controller.UploadFile(null, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task UploadFile_ReturnsBadRequest_WhenFileIsEmpty()
    {
        var controller = new FilesController(_mockStorageService.Object, _options, _mockLogger.Object);
        SetupHttpContext(controller);

        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(0);

        var result = await controller.UploadFile(mockFile.Object, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task UploadFile_ReturnsBadRequest_WhenExtensionIsDisallowed()
    {
        var controller = new FilesController(_mockStorageService.Object, _options, _mockLogger.Object);
        SetupHttpContext(controller);

        var mockFile = CreateMockFormFile("script.exe", "application/x-msdownload", 1024);

        var result = await controller.UploadFile(mockFile.Object, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Contains("not permitted", response.Message);
    }

    [Fact]
    public async Task UploadFile_ReturnsPayloadTooLarge_WhenFileExceedsLimit()
    {
        var controller = new FilesController(_mockStorageService.Object, _options, _mockLogger.Object);
        SetupHttpContext(controller);

        long oversizeInBytes = 15 * 1024 * 1024; // 15MB > 10MB limit
        var mockFile = CreateMockFormFile("large.pdf", "application/pdf", oversizeInBytes);

        var result = await controller.UploadFile(mockFile.Object, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, objectResult.StatusCode);
    }

    [Fact]
    public async Task UploadFile_ReturnsCreatedAtAction_WhenUploadSucceeds()
    {
        var controller = new FilesController(_mockStorageService.Object, _options, _mockLogger.Object);
        SetupHttpContext(controller);

        var mockFile = CreateMockFormFile("document.pdf", "application/pdf", 2048);

        _mockStorageService
            .Setup(s => s.SaveFileAsync(It.IsAny<Stream>(), "document.pdf", "application/pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileUploadResponse
            {
                FileId = "test-guid-123",
                FileName = "test-guid-123.pdf",
                FileSizeInBytes = 2048,
                ContentType = "application/pdf",
                UploadedAtUtc = DateTime.UtcNow
            });

        var result = await controller.UploadFile(mockFile.Object, CancellationToken.None);

        var createdAtResult = Assert.IsType<CreatedAtActionResult>(result);
        var response = Assert.IsType<FileUploadResponse>(createdAtResult.Value);
        Assert.Equal("test-guid-123", response.FileId);
        Assert.Equal("test-guid-123.pdf", response.FileName);
    }

    private static Mock<IFormFile> CreateMockFormFile(string fileName, string contentType, long length)
    {
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns(fileName);
        mockFile.Setup(f => f.ContentType).Returns(contentType);
        mockFile.Setup(f => f.Length).Returns(length);
        mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[length]));
        return mockFile;
    }

    private static void SetupHttpContext(ControllerBase controller)
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "test-trace-id"
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }
}
