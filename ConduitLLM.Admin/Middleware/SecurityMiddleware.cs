using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ConduitLLM.Admin.Interfaces;

namespace ConduitLLM.Admin.Middleware
{
    /// <summary>
    /// Unified security middleware for Admin API that handles authentication, rate limiting, and IP filtering
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
        public async Task InvokeAsync(HttpContext context, ISecurityService securityService)
        {
            var result = await securityService.IsRequestAllowedAsync(context);

            if (!result.IsAllowed)
            {
                _logger.LogWarning("Request blocked: {Reason} for path {Path} from IP {IP}", 
                    result.Reason, 
                    context.Request.Path,
                    context.Connection.RemoteIpAddress);

                context.Response.StatusCode = result.StatusCode ?? 403;
                
                // Add appropriate headers for rate limiting
                if (result.StatusCode == 429)
                {
                    context.Response.Headers.Append("Retry-After", "60");
                    context.Response.Headers.Append("X-RateLimit-Limit", "100"); // Will be made configurable
                }

                // Return JSON error response
                await context.Response.WriteAsJsonAsync(new 
                { 
                    error = result.Reason,
                    statusCode = result.StatusCode
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
        public static IApplicationBuilder UseAdminSecurity(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SecurityMiddleware>();
        }
    }
}