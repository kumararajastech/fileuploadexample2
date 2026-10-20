namespace FileUpload.Api.Models;

/// <summary>
/// Standardized API error response format.
/// </summary>
public class ApiErrorResponse
{
    /// <summary>
    /// HTTP status code.
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Human-readable error message safe for client consumption.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Trace/Correlation identifier for diagnostic tracking.
    /// </summary>
    public string TraceId { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the error occurred.
    /// </summary>
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    public ApiErrorResponse() { }

    public ApiErrorResponse(int statusCode, string message, string traceId)
    {
        StatusCode = statusCode;
        Message = message;
        TraceId = traceId;
        TimestampUtc = DateTime.UtcNow;
    }
}
