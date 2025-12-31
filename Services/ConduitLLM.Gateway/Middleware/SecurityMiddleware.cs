using ConduitLLM.Core.Utilities;
using ConduitLLM.Gateway.Services;
using ConduitLLM.Security.Interfaces;

namespace ConduitLLM.Gateway.Middleware
{
    /// <summary>
    /// Unified security middleware for Gateway API that handles IP filtering, rate limiting, and ban checks
    /// </summary>
    public class SecurityMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<SecurityMiddleware> _logger;

        /// <summary>
        /// Initializes a new instance of the SecurityMiddleware
        /// </summary>
        public SecurityMiddleware(RequestDelegate next, ILogger<SecurityMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        /// <summary>
        /// Processes the HTTP request through security checks
        /// </summary>
        public async Task InvokeAsync(HttpContext context, ISecurityService securityService, ISecurityEventMonitoringService? securityEventMonitoring = null)
        {
            var clientIp = IpAddressHelper.GetClientIpAddress(context);
            var endpoint = context.Request.Path.Value ?? "";

            // Pass along any authentication failure info from VirtualKeyAuthenticationMiddleware
            if (context.Response.StatusCode == 401)
            {
                // Authentication already failed, don't continue
                return;
            }

            var result = await securityService.IsRequestAllowedAsync(context);

            if (!result.IsAllowed)
            {
                _logger.LogWarning("Request blocked: {Reason} for path {Path} from IP {IP}",
                    result.Reason,
                    context.Request.Path,
                    clientIp);

                // Record security events based on the reason
                if (securityEventMonitoring != null)
                {
                    var virtualKey = context.Items["AttemptedKey"] as string ?? "";

                    if (result.Reason.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
                    {
                        var limitType = result.Headers.ContainsKey("X-RateLimit-Scope")
                            ? result.Headers["X-RateLimit-Scope"]
                            : "general";
                        securityEventMonitoring.RecordRateLimitViolation(clientIp, virtualKey, endpoint, limitType);
                    }
                    else if (result.Reason.Contains("banned", StringComparison.OrdinalIgnoreCase))
                    {
                        // IP ban is already recorded by SecurityService
                    }
                    else
                    {
                        securityEventMonitoring.RecordSuspiciousActivity(clientIp, "Access Denied", result.Reason);
                    }
                }

                context.Response.StatusCode = result.StatusCode ?? 403;

                // Add any response headers
                foreach (var header in result.Headers)
                {
                    context.Response.Headers.Append(header.Key, header.Value);
                }

                // Return JSON error response
                await context.Response.WriteAsJsonAsync(new
                {
                    error = result.Reason,
                    code = result.StatusCode
                });
                return;
            }

            await _next(context);
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