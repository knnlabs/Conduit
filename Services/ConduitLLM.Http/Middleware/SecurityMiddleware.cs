using ConduitLLM.Http.Services;
using ConduitLLM.Security.Interfaces;

namespace ConduitLLM.Http.Middleware
{
    /// <summary>
    /// Unified security middleware for Core API that handles IP filtering, rate limiting, and ban checks
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
            var clientIp = GetClientIpAddress(context);
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
                    context.Connection.RemoteIpAddress);

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

        private string GetClientIpAddress(HttpContext context)
        {
            // Check X-Forwarded-For header first (for reverse proxies)
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                // Take the first IP in the chain
                var ip = forwardedFor.Split(',').First().Trim();
                if (System.Net.IPAddress.TryParse(ip, out _))
                {
                    return ip;
                }
            }

            // Check X-Real-IP header
            var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIp) && System.Net.IPAddress.TryParse(realIp, out _))
            {
                return realIp;
            }

            // Fall back to direct connection IP
            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
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