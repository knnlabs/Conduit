using ConduitLLM.Core.Utilities;
using ConduitLLM.Gateway.Services;
using ConduitLLM.Security.Interfaces;
using ConduitLLM.Security.Middleware;
using SecurityModels = ConduitLLM.Security.Models;

namespace ConduitLLM.Gateway.Middleware
{
    /// <summary>
    /// Unified security middleware for Gateway API that handles IP filtering, rate limiting, and ban checks.
    /// Inherits from SecurityMiddlewareBase and adds event monitoring functionality.
    /// </summary>
    public class SecurityMiddleware : SecurityMiddlewareBase
    {
        private ISecurityEventMonitoringService? _securityEventMonitoring;

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
        public async Task InvokeAsync(HttpContext context, ISecurityService securityService, ISecurityEventMonitoringService? securityEventMonitoring = null)
        {
            _securityEventMonitoring = securityEventMonitoring;

            await ProcessRequestAsync(context, async ctx =>
            {
                var result = await securityService.IsRequestAllowedAsync(ctx);

                // Gateway SecurityCheckResult already has Headers, convert to shared type
                return new SecurityModels.SecurityCheckResult
                {
                    IsAllowed = result.IsAllowed,
                    Reason = result.Reason,
                    StatusCode = result.StatusCode,
                    Headers = result.Headers
                };
            });
        }

        /// <summary>
        /// Records security events when a violation occurs (Gateway-specific).
        /// </summary>
        protected override Task OnSecurityViolationAsync(HttpContext context, SecurityModels.SecurityCheckResult result, string clientIp)
        {
            if (_securityEventMonitoring == null)
                return Task.CompletedTask;

            var endpoint = context.Request.Path.Value ?? "";
            var virtualKey = context.Items["AttemptedKey"] as string ?? "";

            if (result.Reason.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
            {
                var limitType = result.Headers.ContainsKey("X-RateLimit-Scope")
                    ? result.Headers["X-RateLimit-Scope"]
                    : "general";
                _securityEventMonitoring.RecordRateLimitViolation(clientIp, virtualKey, endpoint, limitType);
            }
            else if (!result.Reason.Contains("banned", StringComparison.OrdinalIgnoreCase))
            {
                // IP bans are already recorded by SecurityService
                _securityEventMonitoring.RecordSuspiciousActivity(clientIp, "Access Denied", result.Reason);
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