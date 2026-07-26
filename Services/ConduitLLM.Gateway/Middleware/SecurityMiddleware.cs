using ConduitLLM.Security.Middleware;
using ConduitLLM.Security.Models;
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
        /// Logs granular security events.
        /// </summary>
        protected override Task OnSecurityViolationAsync(HttpContext context, SecurityCheckResult result, string clientIp)
        {
            var method = context.Request.Method;
            var path = context.Request.Path.Value ?? "";
            var virtualKey = context.Items["AttemptedKey"] as string ?? "";

            switch (result.StatusCode)
            {
                case 401:
                    Logger.LogWarning(
                        "Security event: AuthenticationFailure — {Method} {Path} from {ClientIp}. Reason: {Reason}",
                        method, path, clientIp, result.Reason);
                    break;
                case 429:
                    Logger.LogWarning(
                        "Security event: RateLimitExceeded — {Method} {Path} from {ClientIp} [VirtualKey: {VirtualKey}]. Reason: {Reason}",
                        method, path, clientIp, virtualKey, result.Reason);
                    break;
                case 403:
                    Logger.LogWarning(
                        "Security event: AccessDenied — {Method} {Path} from {ClientIp}. Reason: {Reason}",
                        method, path, clientIp, result.Reason);
                    break;
                default:
                    Logger.LogWarning(
                        "Security event: Blocked ({StatusCode}) — {Method} {Path} from {ClientIp}. Reason: {Reason}",
                        result.StatusCode, method, path, clientIp, result.Reason);
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
        public static IApplicationBuilder UseCoreApiSecurity(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SecurityMiddleware>();
        }
    }
}
