using ConduitLLM.Core.Extensions;
using System.Diagnostics;

namespace ConduitLLM.Admin.Middleware;

/// <summary>
/// Middleware for tracking Admin API requests
/// </summary>
public class AdminRequestTrackingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AdminRequestTrackingMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the AdminRequestTrackingMiddleware class
    /// </summary>
    /// <param name="next">The next middleware in the pipeline</param>
    /// <param name="logger">Logger</param>
    public AdminRequestTrackingMiddleware(
        RequestDelegate next,
        ILogger<AdminRequestTrackingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Returns true if the HTTP method is a mutation (POST, PUT, PATCH, DELETE).
    /// </summary>
    private static bool IsMutationMethod(string method)
    {
        return method is "POST" or "PUT" or "PATCH" or "DELETE";
    }

    /// <summary>
    /// Processes the request
    /// </summary>
    /// <param name="context">The HTTP context</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestPath = context.Request.Path;
        var requestMethod = context.Request.Method;
        var isMutation = IsMutationMethod(requestMethod);

        try
        {
            // Log request start at Debug — completion log is more useful
            _logger.LogDebug("Admin API Request: {Method} {Path} started",
                LoggingSanitizer.S(requestMethod), LoggingSanitizer.S(requestPath.ToString()));

            // Call the next middleware in the pipeline
            await _next(context);

            stopwatch.Stop();

            // Log mutations and slow requests (>1s) at Information, reads at Debug
            var elapsedMs = stopwatch.ElapsedMilliseconds;
            if (isMutation || elapsedMs > 1000)
            {
                _logger.LogInformation(
                    "Admin API Request: {Method} {Path} completed with status {StatusCode} in {ElapsedMs}ms",
                    LoggingSanitizer.S(requestMethod), LoggingSanitizer.S(requestPath.ToString()), context.Response.StatusCode, elapsedMs);
            }
            else
            {
                _logger.LogDebug(
                    "Admin API Request: {Method} {Path} completed with status {StatusCode} in {ElapsedMs}ms",
                    LoggingSanitizer.S(requestMethod), LoggingSanitizer.S(requestPath.ToString()), context.Response.StatusCode, elapsedMs);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "Admin API Request: {Method} {Path} failed after {ElapsedMs}ms",
                LoggingSanitizer.S(requestMethod), LoggingSanitizer.S(requestPath.ToString()), stopwatch.ElapsedMilliseconds);

            // Re-throw the exception to be handled by the exception handler middleware
            throw;
        }
    }
}
