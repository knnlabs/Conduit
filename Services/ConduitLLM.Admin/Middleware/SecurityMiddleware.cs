using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Metrics;
using ConduitLLM.Security.Middleware;
using SecurityModels = ConduitLLM.Security.Models;

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
            await ProcessRequestAsync(context, async ctx =>
            {
                var result = await securityService.IsRequestAllowedAsync(ctx);

                // Convert Admin SecurityCheckResult to shared SecurityCheckResult
                return new SecurityModels.SecurityCheckResult
                {
                    IsAllowed = result.IsAllowed,
                    Reason = result.Reason,
                    StatusCode = result.StatusCode,
                    // Admin doesn't have Headers, but we can add rate limit headers here
                    Headers = result.StatusCode == 429
                        ? new Dictionary<string, string>
                        {
                            ["Retry-After"] = "60",
                            ["X-RateLimit-Limit"] = "100"
                        }
                        : new Dictionary<string, string>()
                };
            });
        }

        /// <summary>
        /// Logs granular security events distinguishing auth failures, rate limits, and IP blocks.
        /// </summary>
        protected override Task OnSecurityViolationAsync(HttpContext context, SecurityModels.SecurityCheckResult result, string clientIp)
        {
            var method = context.Request.Method;
            var path = context.Request.Path.Value ?? "";

            switch (result.StatusCode)
            {
                case 401:
                    Logger.LogWarning(
                        "Security event: AuthenticationFailure — {Method} {Path} from {ClientIp}. Reason: {Reason}",
                        method, path, clientIp, result.Reason);
                    AdminSecurityMetrics.RecordAuthFailure();
                    break;
                case 429:
                    Logger.LogWarning(
                        "Security event: RateLimitExceeded — {Method} {Path} from {ClientIp}. Reason: {Reason}",
                        method, path, clientIp, result.Reason);
                    AdminSecurityMetrics.RecordRateLimitHit();
                    break;
                case 403:
                    Logger.LogWarning(
                        "Security event: AccessDenied — {Method} {Path} from {ClientIp}. Reason: {Reason}",
                        method, path, clientIp, result.Reason);
                    AdminSecurityMetrics.RecordAccessDenied();
                    break;
                default:
                    Logger.LogWarning(
                        "Security event: Blocked ({StatusCode}) — {Method} {Path} from {ClientIp}. Reason: {Reason}",
                        result.StatusCode, method, path, clientIp, result.Reason);
                    AdminSecurityMetrics.RecordBlocked();
                    break;
            }

            return Task.CompletedTask;
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