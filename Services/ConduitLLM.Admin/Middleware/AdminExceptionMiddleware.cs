using System.Text.Json;

using ConduitLLM.Admin.Extensions;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Extensions;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace ConduitLLM.Admin.Middleware;

/// <summary>
/// Global exception handling middleware for the Admin API.
/// Catches any unhandled exceptions that escape controller-level error handling
/// (e.g., from middleware, model binding, or filters) and returns standardized
/// <see cref="ErrorResponseDto"/> responses.
/// </summary>
/// <remarks>
/// This is a safety net — most exceptions are handled by <see cref="Controllers.AdminControllerBase"/>.
/// This middleware catches anything that slips through, ensuring the Admin API never returns
/// raw exception details to clients.
/// </remarks>
public class AdminExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AdminExceptionMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminExceptionMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="environment">The web host environment.</param>
    public AdminExceptionMiddleware(
        RequestDelegate next,
        ILogger<AdminExceptionMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
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

        // Capture request body for mutation failures (POST/PUT/PATCH/DELETE)
        var requestBody = await RequestBodyCapture.CaptureAsync(context);

        if (requestBody != null)
        {
            _logger.LogError(exception,
                "Unhandled exception caught by AdminExceptionMiddleware. TraceId: {TraceId}, Method: {Method}, Path: {Path}, RequestBody: {RequestBody}",
                traceId,
                LoggingSanitizer.S(context.Request.Method),
                LoggingSanitizer.S(context.Request.Path.ToString()),
                requestBody);
        }
        else
        {
            _logger.LogError(exception,
                "Unhandled exception caught by AdminExceptionMiddleware. TraceId: {TraceId}, Method: {Method}, Path: {Path}",
                traceId,
                LoggingSanitizer.S(context.Request.Method),
                LoggingSanitizer.S(context.Request.Path.ToString()));
        }

        // Map exception using the shared mapper
        var mapping = ExceptionToResponseMapper.Map(exception);

        // In development, show actual exception messages for redacted responses
        var message = mapping.IncludeExceptionMessageInLog
            ? mapping.ResponseMessage
            : (_environment.IsDevelopment() ? exception.Message : mapping.ResponseMessage);

        // Don't try to write if the response has already started
        if (context.Response.HasStarted)
        {
            _logger.LogWarning(
                "Response has already started, cannot write error response for TraceId: {TraceId}",
                traceId);
            return;
        }

        context.Response.StatusCode = mapping.StatusCode;
        context.Response.ContentType = "application/json";

        // Add correlation/trace ID header
        context.Response.Headers["X-Request-Id"] = traceId;

        // Add Retry-After header for rate limit exceptions
        if (exception is RateLimitExceededException rateLimitEx && rateLimitEx.RetryAfterSeconds.HasValue)
        {
            context.Response.Headers["Retry-After"] = rateLimitEx.RetryAfterSeconds.Value.ToString();
        }

        var errorResponse = new ErrorResponseDto(message) { Code = mapping.ErrorCode };

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        var json = JsonSerializer.Serialize(errorResponse, jsonOptions);
        await context.Response.WriteAsync(json);
    }
}

/// <summary>
/// Extension methods for Admin exception middleware.
/// </summary>
public static class AdminExceptionMiddlewareExtensions
{
    /// <summary>
    /// Adds the Admin API global exception handling middleware to the pipeline.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The application builder.</returns>
    public static IApplicationBuilder UseAdminExceptionHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AdminExceptionMiddleware>();
    }
}
