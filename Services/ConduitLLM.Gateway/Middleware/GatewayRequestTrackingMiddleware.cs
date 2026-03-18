using System.Diagnostics;
using ConduitLLM.Core.Extensions;

namespace ConduitLLM.Gateway.Middleware
{
    /// <summary>
    /// Middleware for tracking Gateway API requests with structured logging.
    /// Logs mutations at Information level, slow reads at Information level,
    /// and normal reads at Debug level for operational visibility.
    /// </summary>
    public class GatewayRequestTrackingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GatewayRequestTrackingMiddleware> _logger;

        public GatewayRequestTrackingMiddleware(
            RequestDelegate next,
            ILogger<GatewayRequestTrackingMiddleware> logger)
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

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var requestPath = context.Request.Path;
            var requestMethod = context.Request.Method;

            // Skip health checks to avoid log noise
            if (requestPath.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            var isMutation = IsMutationMethod(requestMethod);

            try
            {
                await _next(context);

                stopwatch.Stop();
                var elapsedMs = stopwatch.ElapsedMilliseconds;
                var virtualKeyId = GetVirtualKeyId(context);

                // Warn on very slow requests (>5s) — may indicate provider issues or timeouts
                if (elapsedMs > 5000)
                {
                    _logger.LogWarning(
                        "Slow Gateway request: {Method} {Path} took {ElapsedMs}ms with status {StatusCode} [VirtualKey: {VirtualKeyId}]",
                        requestMethod, LoggingSanitizer.S(requestPath.ToString()),
                        elapsedMs, context.Response.StatusCode, virtualKeyId);
                }
                // Log mutations and moderately slow requests (>1s) at Information, reads at Debug
                else if (isMutation || elapsedMs > 1000)
                {
                    _logger.LogInformation(
                        "Gateway API Request: {Method} {Path} completed with status {StatusCode} in {ElapsedMs}ms [VirtualKey: {VirtualKeyId}]",
                        requestMethod, LoggingSanitizer.S(requestPath.ToString()),
                        context.Response.StatusCode, elapsedMs, virtualKeyId);
                }
                else
                {
                    _logger.LogDebug(
                        "Gateway API Request: {Method} {Path} completed with status {StatusCode} in {ElapsedMs}ms [VirtualKey: {VirtualKeyId}]",
                        requestMethod, LoggingSanitizer.S(requestPath.ToString()),
                        context.Response.StatusCode, elapsedMs, virtualKeyId);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                _logger.LogError(
                    ex,
                    "Gateway API Request: {Method} {Path} failed after {ElapsedMs}ms [VirtualKey: {VirtualKeyId}]",
                    requestMethod, LoggingSanitizer.S(requestPath.ToString()),
                    stopwatch.ElapsedMilliseconds, GetVirtualKeyId(context));

                throw;
            }
        }

        private static string GetVirtualKeyId(HttpContext context)
        {
            if (context.Items.TryGetValue("VirtualKeyId", out var keyId) && keyId is int id)
            {
                return id.ToString();
            }

            var claim = context.User?.FindFirst("VirtualKeyId")?.Value;
            return claim ?? "anonymous";
        }
    }

    public static class GatewayRequestTrackingMiddlewareExtensions
    {
        public static IApplicationBuilder UseGatewayRequestTracking(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<GatewayRequestTrackingMiddleware>();
        }
    }
}
