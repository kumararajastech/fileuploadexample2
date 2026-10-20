namespace FileUpload.Api.Models;

/// <summary>
/// Response payload returned upon successful file upload.
/// Excludes physical server paths for security reasons.
/// </summary>
public class FileUploadResponse
{
    /// <summary>
    /// Unique server-generated file identifier (GUID).
    /// </summary>
    public string FileId { get; set; } = string.Empty;

    /// <summary>
    /// Sanitized server storage filename.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Size of the uploaded file in bytes.
    /// </summary>
    public long FileSizeInBytes { get; set; }

    /// <summary>
    /// Validated or inferred MIME content type.
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp indicating when the file was saved.
    /// </summary>
    public DateTime UploadedAtUtc { get; set; }
}
