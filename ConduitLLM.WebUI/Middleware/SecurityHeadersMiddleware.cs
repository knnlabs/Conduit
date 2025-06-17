using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

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
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _options = LoadOptionsFromConfiguration(configuration);
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
        if (_options.EnableXFrameOptions && !headers.ContainsKey("X-Frame-Options"))
        {
            headers.Append("X-Frame-Options", _options.XFrameOptions);
        }

        // X-Content-Type-Options - Prevent MIME type sniffing
        if (_options.EnableXContentTypeOptions && !headers.ContainsKey("X-Content-Type-Options"))
        {
            headers.Append("X-Content-Type-Options", "nosniff");
        }

        // X-XSS-Protection - Enable XSS filtering (for older browsers)
        if (_options.EnableXXssProtection && !headers.ContainsKey("X-XSS-Protection"))
        {
            headers.Append("X-XSS-Protection", "1; mode=block");
        }

        // Referrer-Policy
        if (_options.EnableReferrerPolicy && !headers.ContainsKey("Referrer-Policy"))
        {
            headers.Append("Referrer-Policy", _options.ReferrerPolicy);
        }

        // Content-Security-Policy
        if (_options.EnableContentSecurityPolicy && !string.IsNullOrWhiteSpace(_options.ContentSecurityPolicy) && !headers.ContainsKey("Content-Security-Policy"))
        {
            headers.Append("Content-Security-Policy", _options.ContentSecurityPolicy);
        }

        // Strict-Transport-Security (HSTS) - Only for HTTPS
        if (_options.EnableHsts && context.Request.IsHttps && !headers.ContainsKey("Strict-Transport-Security"))
        {
            headers.Append("Strict-Transport-Security", $"max-age={_options.HstsMaxAge}; includeSubDomains");
        }

        // Permissions-Policy (formerly Feature-Policy)
        if (_options.EnablePermissionsPolicy && !string.IsNullOrWhiteSpace(_options.PermissionsPolicy) && !headers.ContainsKey("Permissions-Policy"))
        {
            headers.Append("Permissions-Policy", _options.PermissionsPolicy);
        }

        // Remove potentially dangerous headers
        headers.Remove("X-Powered-By");
        headers.Remove("Server");

        _logger.LogDebug("Security headers added to response for {Path}", context.Request.Path);
    }

    private SecurityHeadersOptions LoadOptionsFromConfiguration(IConfiguration configuration)
    {
        var options = new SecurityHeadersOptions();

        // Load from environment variables with CONDUIT_ prefix
        options.EnableXFrameOptions = configuration.GetValue<bool>("CONDUIT_SECURITY_HEADERS_X_FRAME_OPTIONS_ENABLED", true);
        options.XFrameOptions = configuration["CONDUIT_SECURITY_HEADERS_X_FRAME_OPTIONS"] ?? "DENY";

        options.EnableXContentTypeOptions = configuration.GetValue<bool>("CONDUIT_SECURITY_HEADERS_X_CONTENT_TYPE_OPTIONS_ENABLED", true);
        options.EnableXXssProtection = configuration.GetValue<bool>("CONDUIT_SECURITY_HEADERS_X_XSS_PROTECTION_ENABLED", true);

        options.EnableReferrerPolicy = configuration.GetValue<bool>("CONDUIT_SECURITY_HEADERS_REFERRER_POLICY_ENABLED", true);
        options.ReferrerPolicy = configuration["CONDUIT_SECURITY_HEADERS_REFERRER_POLICY"] ?? "strict-origin-when-cross-origin";

        options.EnableContentSecurityPolicy = configuration.GetValue<bool>("CONDUIT_SECURITY_HEADERS_CSP_ENABLED", true);
        options.ContentSecurityPolicy = configuration["CONDUIT_SECURITY_HEADERS_CSP"] ?? 
            "default-src 'self' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://fonts.googleapis.com https://fonts.gstatic.com; " +
            "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com; " +
            "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://fonts.googleapis.com; " +
            "img-src 'self' data: https:; " +
            "font-src 'self' https://cdnjs.cloudflare.com https://fonts.gstatic.com; " +
            "connect-src 'self' wss: ws:; " +
            "frame-ancestors 'none';";

        options.EnableHsts = configuration.GetValue<bool>("CONDUIT_SECURITY_HEADERS_HSTS_ENABLED", true);
        options.HstsMaxAge = configuration.GetValue<int>("CONDUIT_SECURITY_HEADERS_HSTS_MAX_AGE", 31536000); // 1 year

        options.EnablePermissionsPolicy = configuration.GetValue<bool>("CONDUIT_SECURITY_HEADERS_PERMISSIONS_POLICY_ENABLED", true);
        options.PermissionsPolicy = configuration["CONDUIT_SECURITY_HEADERS_PERMISSIONS_POLICY"] ?? 
            "camera=(), microphone=(), geolocation=(), payment=()";

        return options;
    }

    private class SecurityHeadersOptions
    {
        public bool EnableXFrameOptions { get; set; } = true;
        public string XFrameOptions { get; set; } = "DENY";
        
        public bool EnableXContentTypeOptions { get; set; } = true;
        public bool EnableXXssProtection { get; set; } = true;
        
        public bool EnableReferrerPolicy { get; set; } = true;
        public string ReferrerPolicy { get; set; } = "strict-origin-when-cross-origin";
        
        public bool EnableContentSecurityPolicy { get; set; } = true;
        public string ContentSecurityPolicy { get; set; } = "";
        
        public bool EnableHsts { get; set; } = true;
        public int HstsMaxAge { get; set; } = 31536000; // 1 year
        
        public bool EnablePermissionsPolicy { get; set; } = true;
        public string PermissionsPolicy { get; set; } = "";
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