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
    /// Processes the request
    /// </summary>
    /// <param name="context">The HTTP context</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestPath = context.Request.Path;
        var requestMethod = context.Request.Method;

        try
        {
            _logger.LogInformation("Admin API Request: {Method} {Path} started", requestMethod, requestPath);
            
            // Call the next middleware in the pipeline
            await _next(context);
            
            stopwatch.Stop();
            
            _logger.LogInformation(
                "Admin API Request: {Method} {Path} completed with status {StatusCode} in {ElapsedMs}ms",
                requestMethod, requestPath, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _logger.LogError(
                ex, 
                "Admin API Request: {Method} {Path} failed after {ElapsedMs}ms",
                requestMethod, requestPath, stopwatch.ElapsedMilliseconds);
            
            // Re-throw the exception to be handled by the exception handler middleware
            throw;
        }
    }
}