using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ConduitLLM.WebUI.Services;

namespace ConduitLLM.WebUI.Middleware
{
    /// <summary>
    /// Unified security middleware that handles all security checks
    /// </summary>
    public class SecurityMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<SecurityMiddleware> _logger;

        public SecurityMiddleware(RequestDelegate next, ILogger<SecurityMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

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
                }

                await context.Response.WriteAsync(result.Reason);
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
        public static IApplicationBuilder UseSecurity(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SecurityMiddleware>();
        }
    }
}