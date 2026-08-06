using ConduitLLM.Gateway.Metrics;
using ConduitLLM.Security.Middleware;
using ISecurityService = ConduitLLM.Security.Interfaces.ISecurityService;

namespace ConduitLLM.Gateway.Middleware
{
    /// <summary>
    /// Unified security middleware for Gateway API that handles IP filtering, rate limiting, and ban checks.
    /// Inherits from SecurityMiddlewareBase and adds granular violation logging.
    /// </summary>
    public class SecurityMiddleware : SecurityMiddlewareBase
    {
        /// <summary>
        /// Initializes a new instance of the SecurityMiddleware
        /// </summary>
        public SecurityMiddleware(RequestDelegate next, ILogger<SecurityMiddleware> logger)
            : base(next, logger)
        {
        }

        /// <summary>
        /// Processes the HTTP request through security checks
        /// </summary>
        public async Task InvokeAsync(HttpContext context, ISecurityService securityService)
        {
            await ProcessRequestAsync(context, ctx => securityService.IsRequestAllowedAsync(ctx));
        }

        /// <summary>
        /// Records Gateway API security violation metrics.
        /// </summary>
        protected override void RecordViolationMetric(int statusCode)
        {
            switch (statusCode)
            {
                case StatusCodes.Status401Unauthorized:
                    GatewaySecurityMetrics.RecordAuthFailure();
                    break;
                case StatusCodes.Status429TooManyRequests:
                    GatewaySecurityMetrics.RecordRateLimitHit();
                    break;
                case StatusCodes.Status403Forbidden:
                    GatewaySecurityMetrics.RecordAccessDenied();
                    break;
                default:
                    GatewaySecurityMetrics.RecordBlocked();
                    break;
            }
        }
    }

    /// <summary>
    /// Extension methods for SecurityMiddleware
    /// </summary>
    public static class SecurityMiddlewareExtensions
    {
        /// <summary>
        /// Adds the security middleware to the pipeline
        /// </summary>
        public static IApplicationBuilder UseCoreApiSecurity(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SecurityMiddleware>();
        }
    }
}
