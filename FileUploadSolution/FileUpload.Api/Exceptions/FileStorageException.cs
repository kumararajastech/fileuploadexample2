namespace FileUpload.Api.Exceptions;

/// <summary>
/// Domain exception thrown when file storage operations fail.
/// </summary>
public class FileStorageException : Exception
{
    public int StatusCode { get; }

    public FileStorageException(string message, int statusCode = 500) : base(message)
    {
        StatusCode = statusCode;
    }

    public FileStorageException(string message, Exception innerException, int statusCode = 500) 
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}
