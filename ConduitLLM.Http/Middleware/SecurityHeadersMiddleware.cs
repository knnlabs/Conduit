using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using ConduitLLM.Http.Options;

namespace ConduitLLM.Http.Middleware
{
    /// <summary>
    /// Middleware that adds security headers to HTTP responses for the Core API
    /// </summary>
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<SecurityHeadersMiddleware> _logger;
        private readonly SecurityHeadersOptions _options;

        /// <summary>
        /// Initializes a new instance of the SecurityHeadersMiddleware
        /// </summary>
        public SecurityHeadersMiddleware(
            RequestDelegate next,
            ILogger<SecurityHeadersMiddleware> logger,
            IOptions<SecurityOptions> securityOptions)
        {
            _next = next;
            _logger = logger;
            _options = securityOptions.Value.Headers;
        }

        /// <summary>
        /// Adds security headers to the HTTP response
        /// </summary>
        public async Task InvokeAsync(HttpContext context)
        {
            // Add security headers before processing the request
            AddSecurityHeaders(context);

            await _next(context);
        }

        private void AddSecurityHeaders(HttpContext context)
        {
            var headers = context.Response.Headers;

            // X-Content-Type-Options - Prevent MIME type sniffing
            if (_options.XContentTypeOptions && !headers.ContainsKey("X-Content-Type-Options"))
            {
                headers.Append("X-Content-Type-Options", "nosniff");
            }

            // X-XSS-Protection - Usually not needed for APIs but configurable
            if (_options.XXssProtection && !headers.ContainsKey("X-XSS-Protection"))
            {
                headers.Append("X-XSS-Protection", "1; mode=block");
            }

            // Strict-Transport-Security (HSTS) - Only for HTTPS
            if (_options.Hsts.Enabled && context.Request.IsHttps && !headers.ContainsKey("Strict-Transport-Security"))
            {
                headers.Append("Strict-Transport-Security", $"max-age={_options.Hsts.MaxAge}; includeSubDomains");
            }

            // Add custom headers
            foreach (var customHeader in _options.CustomHeaders)
            {
                if (!headers.ContainsKey(customHeader.Key))
                {
                    headers.Append(customHeader.Key, customHeader.Value);
                }
            }

            // Remove potentially dangerous headers
            headers.Remove("X-Powered-By");
            headers.Remove("Server");

            // Add API-specific headers
            if (!headers.ContainsKey("X-Content-Type"))
            {
                headers.Append("X-Content-Type", "application/json");
            }
            
            // API version header
            if (!headers.ContainsKey("X-API-Version"))
            {
                headers.Append("X-API-Version", "v1");
            }

            _logger.LogDebug("Security headers added to response for {Path}", context.Request.Path);
        }
    }

    /// <summary>
    /// Extension methods for adding security headers middleware
    /// </summary>
    public static class SecurityHeadersMiddlewareExtensions
    {
        /// <summary>
        /// Adds security headers middleware to the application pipeline
        /// </summary>
        public static IApplicationBuilder UseCoreApiSecurityHeaders(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SecurityHeadersMiddleware>();
        }
    }
}