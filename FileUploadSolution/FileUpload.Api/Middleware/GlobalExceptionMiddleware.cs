using System.Net;
using System.Text.Json;
using FileUpload.Api.Exceptions;
using FileUpload.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FileUpload.Api.Middleware;

/// <summary>
/// Centralized exception handling middleware to prevent exposing stack traces and internal paths to clients.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = context.TraceIdentifier;
        int statusCode = (int)HttpStatusCode.InternalServerError;
        string clientMessage = "An error occurred while processing your request.";

        if (exception is FileStorageException storageException)
        {
            statusCode = storageException.StatusCode;
            clientMessage = storageException.Message;
            _logger.LogWarning(exception, "Domain Exception handled [TraceId: {TraceId}]: {Message}", traceId, exception.Message);
        }
        else if (exception is BadHttpRequestException badRequestEx)
        {
            statusCode = (int)HttpStatusCode.BadRequest;
            clientMessage = badRequestEx.Message;
            _logger.LogWarning(badRequestEx, "Bad Request [TraceId: {TraceId}]: {Message}", traceId, badRequestEx.Message);
        }
        else if (exception is OperationCanceledException)
        {
            statusCode = 499; // Client Closed Request
            clientMessage = "Request was canceled.";
            _logger.LogInformation("Request canceled [TraceId: {TraceId}]", traceId);
        }
        else
        {
            _logger.LogError(exception, "Unhandled Exception caught in middleware [TraceId: {TraceId}]", traceId);
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var errorResponse = new ApiErrorResponse(statusCode, clientMessage, traceId);
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        return context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse, jsonOptions));
    }
}
