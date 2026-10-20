using FileUpload.Api.Exceptions;
using FileUpload.Api.Models;
using FileUpload.Api.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FileUpload.Api.Services;

/// <summary>
/// Production storage service implementing file persistence to network shared folders (UNC) or local directories.
/// </summary>
public class SharedFolderStorageService : IFileStorageService
{
    private readonly FileStorageOptions _options;
    private readonly ILogger<SharedFolderStorageService> _logger;

    public SharedFolderStorageService(
        IOptions<FileStorageOptions> options,
        ILogger<SharedFolderStorageService> logger)
    {
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<FileUploadResponse> SaveFileAsync(
        Stream fileStream,
        string originalFileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (fileStream == null || fileStream.Length == 0)
        {
            throw new FileStorageException("Cannot save an empty file stream.", 400);
        }

        var folderPath = _options.SharedFolderPath;
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            _logger.LogError("Shared folder path is not configured.");
            throw new FileStorageException("File storage path configuration is missing.", 500);
        }

        // Verify target directory availability without silent auto-creation on invalid network paths
        if (!Directory.Exists(folderPath))
        {
            _logger.LogError("Target storage directory '{FolderPath}' is inaccessible or does not exist.", folderPath);
            throw new FileStorageException("Storage location is unavailable. Check share path and network permissions.", 503);
        }

        // Validate extension against configured allowlist
        var extension = Path.GetExtension(originalFileName)?.ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !_options.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogWarning("File upload rejected due to disallowed extension: '{Extension}'", extension);
            throw new FileStorageException($"File extension '{extension}' is not allowed.", 400);
        }

        // Generate safe unique server-side filename (GUID + validated extension)
        var fileId = Guid.NewGuid().ToString("N");
        var safeServerFileName = $"{fileId}{extension}";

        // Secure Path Construction & Path Traversal Prevention
        var destinationFullPath = Path.GetFullPath(Path.Combine(folderPath, safeServerFileName));
        var baseFullPath = Path.GetFullPath(folderPath);

        if (!destinationFullPath.StartsWith(baseFullPath, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogCritical("Path traversal attack detected for filename: '{FileName}'", originalFileName);
            throw new FileStorageException("Invalid target file path.", 400);
        }

        // Prevent file overwrite
        if (File.Exists(destinationFullPath))
        {
            _logger.LogError("File collision detected for server filename '{FileName}'", safeServerFileName);
            throw new FileStorageException("File already exists in storage destination.", 409);
        }

        long bytesWritten = 0;
        try
        {
            _logger.LogInformation("Starting file upload stream for FileId: {FileId}", fileId);

            // Stream directly to destination with buffer to avoid loading entire file in RAM
            const int bufferSize = 81920; // 80 KB buffer
            using (var destinationStream = new FileStream(
                destinationFullPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize,
                useAsync: true))
            {
                await fileStream.CopyToAsync(destinationStream, bufferSize, cancellationToken);
                bytesWritten = destinationStream.Length;
            }

            _logger.LogInformation("Successfully stored file. FileId: {FileId}, Size: {Bytes} bytes", fileId, bytesWritten);

            return new FileUploadResponse
            {
                FileId = fileId,
                FileName = safeServerFileName,
                FileSizeInBytes = bytesWritten,
                ContentType = contentType,
                UploadedAtUtc = DateTime.UtcNow
            };
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning("File upload operation was canceled for FileId: {FileId}", fileId);
            CleanUpPartialFile(destinationFullPath);
            throw new FileStorageException("File upload operation was canceled.", ex, 499);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied to storage path '{Path}' for FileId: {FileId}", folderPath, fileId);
            CleanUpPartialFile(destinationFullPath);
            throw new FileStorageException("Access denied to file storage destination.", ex, 403);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "I/O error while persisting file for FileId: {FileId}", fileId);
            CleanUpPartialFile(destinationFullPath);
            throw new FileStorageException("Storage I/O failure occurred while writing file.", ex, 500);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred during storage of FileId: {FileId}", fileId);
            CleanUpPartialFile(destinationFullPath);
            throw new FileStorageException("An error occurred while persisting the file.", ex, 500);
        }
    }

    private void CleanUpPartialFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("Cleaned up partial file: {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete incomplete file at {FilePath}", filePath);
        }
    }
}
