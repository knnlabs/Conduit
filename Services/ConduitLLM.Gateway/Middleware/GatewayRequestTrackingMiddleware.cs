using ConduitLLM.Core.Middleware;

namespace ConduitLLM.Gateway.Middleware
{
    /// <summary>
    /// Middleware for tracking Gateway API requests with structured logging.
    /// Logs mutations at Information level, slow reads at Information level,
    /// and normal reads at Debug level for operational visibility.
    /// </summary>
    public class GatewayRequestTrackingMiddleware : RequestTrackingMiddlewareBase
    {
        public GatewayRequestTrackingMiddleware(
            RequestDelegate next,
            ILogger<GatewayRequestTrackingMiddleware> logger)
            : base(next, logger) { }

        protected override string ServiceName => "Gateway API";

        protected override int SlowRequestWarningThresholdMs => 5000;

        protected override bool ShouldSkipRequest(HttpContext context)
        {
            return context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);
        }

        protected override string? GetRequestIdentifier(HttpContext context)
        {
            if (context.Items.TryGetValue("VirtualKeyId", out var keyId) && keyId is int id)
            {
                return id.ToString();
            }

            return context.User?.FindFirst("VirtualKeyId")?.Value ?? "anonymous";
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
