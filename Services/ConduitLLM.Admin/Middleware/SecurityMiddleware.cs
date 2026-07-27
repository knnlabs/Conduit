using ConduitLLM.Admin.Metrics;
using ConduitLLM.Security.Middleware;
using ISecurityService = ConduitLLM.Security.Interfaces.ISecurityService;

namespace ConduitLLM.Admin.Middleware
{
    /// <summary>
    /// Unified security middleware for Admin API that handles authentication, rate limiting, and IP filtering.
    /// Inherits from SecurityMiddlewareBase for common functionality.
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
        /// Records Admin API security violation metrics.
        /// </summary>
        protected override void RecordViolationMetric(int statusCode)
        {
            switch (statusCode)
            {
                case StatusCodes.Status401Unauthorized:
                    AdminSecurityMetrics.RecordAuthFailure();
                    break;
                case StatusCodes.Status429TooManyRequests:
                    AdminSecurityMetrics.RecordRateLimitHit();
                    break;
                case StatusCodes.Status403Forbidden:
                    AdminSecurityMetrics.RecordAccessDenied();
                    break;
                default:
                    AdminSecurityMetrics.RecordBlocked();
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
        public static IApplicationBuilder UseAdminSecurity(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SecurityMiddleware>();
        }
    }
}
