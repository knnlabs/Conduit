using System.Text.Json;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Extensions;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Middleware;

/// <summary>
/// Base middleware for global exception handling. Catches unhandled exceptions,
/// logs them with request context, maps them via <see cref="ExceptionToResponseMapper"/>,
/// and writes a JSON error response.
/// Subclasses control the response format (e.g., <c>ErrorResponseDto</c> vs <c>OpenAIErrorResponse</c>).
/// </summary>
public abstract class ExceptionHandlingMiddlewareBase
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _environment;
    protected readonly ILogger Logger;

    protected static readonly JsonSerializerOptions ErrorJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Display name used in log messages (e.g., "AdminExceptionMiddleware").
    /// </summary>
    protected abstract string MiddlewareName { get; }

    protected ExceptionHandlingMiddlewareBase(
        RequestDelegate next,
        ILogger logger,
        IWebHostEnvironment environment)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
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

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = context.TraceIdentifier;

        // Capture request body for debugging mutations
        string? requestBody = null;
        try
        {
            requestBody = await RequestBodyCapture.CaptureAsync(context);
        }
        catch
        {
            // Body capture should never prevent error handling
        }

        // Log with or without body
        if (requestBody != null)
        {
            Logger.LogError(exception,
                "Unhandled exception caught by {MiddlewareName}. TraceId: {TraceId}, Method: {Method}, Path: {Path}, RequestBody: {RequestBody}",
                MiddlewareName, traceId,
                LoggingSanitizer.S(context.Request.Method),
                LoggingSanitizer.S(context.Request.Path.ToString()),
                requestBody);
        }
        else
        {
            Logger.LogError(exception,
                "Unhandled exception caught by {MiddlewareName}. TraceId: {TraceId}, Method: {Method}, Path: {Path}",
                MiddlewareName, traceId,
                LoggingSanitizer.S(context.Request.Method),
                LoggingSanitizer.S(context.Request.Path.ToString()));
        }

        // Map exception using the shared mapper
        var mapping = ExceptionToResponseMapper.Map(exception);

        // In development, show actual exception messages for redacted responses
        var message = mapping.IncludeExceptionMessageInLog
            ? mapping.ResponseMessage
            : (_environment.IsDevelopment() ? exception.Message : mapping.ResponseMessage);

        // Hook for subclass-specific behavior (metrics, security logging)
        await OnExceptionMappedAsync(context, exception, mapping);

        // Don't try to write if the response has already started
        if (context.Response.HasStarted)
        {
            Logger.LogWarning(
                "Response has already started, cannot write error response for TraceId: {TraceId}",
                traceId);
            return;
        }

        // Set common response headers
        context.Response.StatusCode = mapping.StatusCode;
        context.Response.ContentType = "application/json";
        context.Response.Headers["X-Request-Id"] = traceId;

        if (exception is RateLimitExceededException rateLimitEx && rateLimitEx.RetryAfterSeconds.HasValue)
        {
            context.Response.Headers["Retry-After"] = rateLimitEx.RetryAfterSeconds.Value.ToString();
        }

        // Serialize and write the format-specific response
        var json = CreateErrorResponseJson(message, mapping);
        await context.Response.WriteAsync(json);
    }

    /// <summary>
    /// Called after the exception is mapped but before the response is written.
    /// Override to add metrics, security logging, etc.
    /// </summary>
    protected virtual Task OnExceptionMappedAsync(
        HttpContext context,
        Exception exception,
        ExceptionToResponseMapper.ExceptionMappingResult mapping)
        => Task.CompletedTask;

    /// <summary>
    /// Creates the JSON response body for the error. Subclasses produce their format
    /// (e.g., <c>ErrorResponseDto</c> or <c>OpenAIErrorResponse</c>).
    /// </summary>
    protected abstract string CreateErrorResponseJson(
        string message,
        ExceptionToResponseMapper.ExceptionMappingResult mapping);
}
