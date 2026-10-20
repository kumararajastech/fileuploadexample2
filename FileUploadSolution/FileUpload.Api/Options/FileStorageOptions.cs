using System.ComponentModel.DataAnnotations;

namespace FileUpload.Api.Options;

/// <summary>
/// Strongly-typed configuration options for shared folder file storage.
/// </summary>
public class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>
    /// Path to the shared folder (UNC path like \\FileServer\Share or absolute local path).
    /// </summary>
    [Required(ErrorMessage = "SharedFolderPath is required.")]
    public string SharedFolderPath { get; set; } = string.Empty;

    /// <summary>
    /// Maximum permitted file size in Megabytes.
    /// </summary>
    [Range(1, 2048, ErrorMessage = "MaxFileSizeInMB must be between 1 MB and 2048 MB.")]
    public int MaxFileSizeInMB { get; set; } = 20;

    /// <summary>
    /// List of allowed file extensions (must start with a dot, e.g., '.pdf').
    /// </summary>
    [Required(ErrorMessage = "AllowedExtensions list cannot be empty.")]
    public string[] AllowedExtensions { get; set; } = Array.Empty<string>();
}
