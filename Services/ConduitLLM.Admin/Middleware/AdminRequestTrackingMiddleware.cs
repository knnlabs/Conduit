using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Middleware;

namespace ConduitLLM.Admin.Middleware;

/// <summary>
/// Middleware for tracking Admin API requests with structured logging.
/// </summary>
public class AdminRequestTrackingMiddleware : RequestTrackingMiddlewareBase
{
    public AdminRequestTrackingMiddleware(
        RequestDelegate next,
        ILogger<AdminRequestTrackingMiddleware> logger)
        : base(next, logger) { }

    protected override string ServiceName => "Admin API";

    protected override void OnBeforeRequest(HttpContext context, string method, string path)
    {
        Logger.LogDebug("Admin API Request: {Method} {Path} started",
            LoggingSanitizer.S(method), path);
    }
}
