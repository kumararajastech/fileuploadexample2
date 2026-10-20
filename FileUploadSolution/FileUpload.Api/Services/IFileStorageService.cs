using FileUpload.Api.Models;

namespace FileUpload.Api.Services;

/// <summary>
/// Contract for storing and managing files in designated storage destinations.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Asynchronously streams an uploaded file stream into the configured target storage.
    /// </summary>
    /// <param name="fileStream">The input file stream.</param>
    /// <param name="originalFileName">Original file name provided by the client.</param>
    /// <param name="contentType">Validated content type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Metadata of the successfully stored file.</returns>
    Task<FileUploadResponse> SaveFileAsync(
        Stream fileStream,
        string originalFileName,
        string contentType,
        CancellationToken cancellationToken = default);
}
