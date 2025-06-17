using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using ConduitLLM.WebUI.Options;

namespace ConduitLLM.WebUI.Middleware;

/// <summary>
/// Middleware that adds security headers to HTTP responses
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityHeadersMiddleware> _logger;
    private readonly SecurityHeadersOptions _options;

    public SecurityHeadersMiddleware(
        RequestDelegate next,
        ILogger<SecurityHeadersMiddleware> logger,
        IOptions<SecurityOptions> securityOptions)
    {
        _next = next;
        _logger = logger;
        _options = securityOptions.Value.Headers;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add security headers before processing the request
        AddSecurityHeaders(context);

        await _next(context);
    }

    private void AddSecurityHeaders(HttpContext context)
    {
        var headers = context.Response.Headers;

        // X-Frame-Options - Prevent clickjacking
        if (_options.XFrameOptions.Enabled && !headers.ContainsKey("X-Frame-Options"))
        {
            headers.Append("X-Frame-Options", _options.XFrameOptions.Value);
        }

        // X-Content-Type-Options - Prevent MIME type sniffing
        if (_options.XContentTypeOptions && !headers.ContainsKey("X-Content-Type-Options"))
        {
            headers.Append("X-Content-Type-Options", "nosniff");
        }

        // X-XSS-Protection - Enable XSS filtering (for older browsers)
        if (_options.XXssProtection && !headers.ContainsKey("X-XSS-Protection"))
        {
            headers.Append("X-XSS-Protection", "1; mode=block");
        }

        // Referrer-Policy
        if (_options.ReferrerPolicy.Enabled && !headers.ContainsKey("Referrer-Policy"))
        {
            headers.Append("Referrer-Policy", _options.ReferrerPolicy.Value);
        }

        // Content-Security-Policy
        if (_options.ContentSecurityPolicy.Enabled && !string.IsNullOrWhiteSpace(_options.ContentSecurityPolicy.Value) && !headers.ContainsKey("Content-Security-Policy"))
        {
            headers.Append("Content-Security-Policy", _options.ContentSecurityPolicy.Value);
        }

        // Strict-Transport-Security (HSTS) - Only for HTTPS
        if (_options.Hsts.Enabled && context.Request.IsHttps && !headers.ContainsKey("Strict-Transport-Security"))
        {
            headers.Append("Strict-Transport-Security", $"max-age={_options.Hsts.MaxAge}; includeSubDomains");
        }

        // Permissions-Policy (formerly Feature-Policy)
        if (_options.PermissionsPolicy.Enabled && !string.IsNullOrWhiteSpace(_options.PermissionsPolicy.Value) && !headers.ContainsKey("Permissions-Policy"))
        {
            headers.Append("Permissions-Policy", _options.PermissionsPolicy.Value);
        }

        // Remove potentially dangerous headers
        headers.Remove("X-Powered-By");
        headers.Remove("Server");

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
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityHeadersMiddleware>();
    }
}