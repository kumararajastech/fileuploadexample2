using FileUpload.Api.Models;
using FileUpload.Api.Options;
using FileUpload.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FileUpload.Api.Controllers;

/// <summary>
/// Manages file upload endpoints for shared folder storage.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class FilesController : ControllerBase
{
    private readonly IFileStorageService _storageService;
    private readonly FileStorageOptions _options;
    private readonly ILogger<FilesController> _logger;

    public FilesController(
        IFileStorageService storageService,
        IOptions<FileStorageOptions> options,
        ILogger<FilesController> logger)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Uploads a single file to the designated shared storage folder.
    /// </summary>
    /// <param name="file">The file payload supplied via multipart/form-data.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>Metadata of the uploaded file.</returns>
    /// <response code="201">File successfully uploaded and stored.</response>
    /// <response code="400">Missing file, zero-byte file, or disallowed extension.</response>
    /// <response code="413">File size exceeds maximum permitted limit.</response>
    /// <response code="500">Internal storage error.</response>
    /// <response code="503">Shared folder storage location unavailable.</response>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(FileUploadResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UploadFile(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            _logger.LogWarning("Upload request rejected: File parameter is null or zero-length.");
            return BadRequest(new ApiErrorResponse(
                StatusCodes.Status400BadRequest,
                "Please select a non-empty file to upload.",
                HttpContext.TraceIdentifier));
        }

        // Validate File Size against MaxFileSizeInMB
        long maxFileSizeBytes = (long)_options.MaxFileSizeInMB * 1024 * 1024;
        if (file.Length > maxFileSizeBytes)
        {
            _logger.LogWarning("File '{FileName}' ({SizeBytes} bytes) exceeds limit of {MaxMB} MB.",
                file.FileName, file.Length, _options.MaxFileSizeInMB);

            return StatusCode(StatusCodes.Status413PayloadTooLarge, new ApiErrorResponse(
                StatusCodes.Status413PayloadTooLarge,
                $"File size exceeds the maximum allowed limit of {_options.MaxFileSizeInMB} MB.",
                HttpContext.TraceIdentifier));
        }

        // Validate File Extension against Allowlist
        var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !_options.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogWarning("File '{FileName}' has invalid extension '{Extension}'.", file.FileName, extension);
            return BadRequest(new ApiErrorResponse(
                StatusCodes.Status400BadRequest,
                $"File extension '{extension}' is not permitted.",
                HttpContext.TraceIdentifier));
        }

        // Stream file directly to storage service
        using var stream = file.OpenReadStream();
        var result = await _storageService.SaveFileAsync(
            stream,
            file.FileName,
            file.ContentType,
            cancellationToken);

        return CreatedAtAction(nameof(UploadFile), new { id = result.FileId }, result);
    }
}
