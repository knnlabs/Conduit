using System.Net;
using System.Text.Json;
using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.Options;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.WebUI.Middleware;

/// <summary>
/// Middleware for enforcing IP address filtering rules
/// </summary>
public class IpFilterMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IpFilterMiddleware> _logger;
    private readonly IOptions<IpFilterOptions> _options;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="IpFilterMiddleware"/> class
    /// </summary>
    /// <param name="next">The next middleware in the pipeline</param>
    /// <param name="logger">Logger for the middleware</param>
    /// <param name="options">IP filtering configuration options</param>
    public IpFilterMiddleware(
        RequestDelegate next,
        ILogger<IpFilterMiddleware> logger,
        IOptions<IpFilterOptions> options)
    {
        _next = next;
        _logger = logger;
        _options = options;
    }
    
    /// <summary>
    /// Invokes the middleware
    /// </summary>
    /// <param name="context">The HTTP context</param>
    /// <param name="ipFilterService">The IP filter service</param>
    /// <returns>A task representing the middleware execution</returns>
    public async Task InvokeAsync(
        HttpContext context,
        IIpFilterService ipFilterService)
    {
        try
        {
            var settings = await ipFilterService.GetIpFilterSettingsAsync();
            
            // Skip filtering if it's disabled in settings
            if (!settings.IsEnabled)
            {
                await _next(context);
                return;
            }
            
            // Skip filtering for excluded paths
            string path = context.Request.Path.Value ?? string.Empty;
            if (IsExcludedPath(path, settings.ExcludedEndpoints))
            {
                _logger.LogDebug("Skipping IP filtering for excluded path: {Path}", path);
                await _next(context);
                return;
            }
            
            // Skip filtering for admin UI access if configured
            if (settings.BypassForAdminUi && !path.StartsWith("/api/"))
            {
                _logger.LogDebug("Skipping IP filtering for admin UI access: {Path}", path);
                await _next(context);
                return;
            }
            
            // Get the client IP address
            string? clientIp = GetClientIpAddress(context);
            if (string.IsNullOrEmpty(clientIp))
            {
                _logger.LogWarning("Could not determine client IP address. Applying default allow setting: {DefaultAllow}", 
                    settings.DefaultAllow);
                    
                if (settings.DefaultAllow)
                {
                    await _next(context);
                }
                else
                {
                    await RespondWithForbidden(context, "Could not determine client IP address");
                }
                
                return;
            }
            
            // Check if the IP is allowed
            bool isAllowed = await ipFilterService.IsIpAllowedAsync(clientIp);
            if (isAllowed)
            {
                _logger.LogDebug("IP {ClientIp} is allowed access", clientIp);
                await _next(context);
            }
            else
            {
                _logger.LogInformation("IP {ClientIp} is blocked from accessing {Path}", clientIp, path);
                await RespondWithForbidden(context, IpFilterConstants.ACCESS_DENIED_MESSAGE);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in IP filtering middleware");
            
            // In case of error, continue to the next middleware
            // This ensures that IP filtering doesn't break the application if there's an issue
            await _next(context);
        }
    }
    
    private bool IsExcludedPath(string path, IEnumerable<string> excludedEndpoints)
    {
        foreach (var excludedPath in excludedEndpoints)
        {
            if (path.StartsWith(excludedPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        
        return false;
    }
    
    private static string? GetClientIpAddress(HttpContext context)
    {
        // Check for X-Forwarded-For header
        string? forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // The client IP is the first one in the list
            string clientIp = forwardedFor.Split(',')[0].Trim();
            return clientIp;
        }
        
        // Fallback to connection remote IP
        return context.Connection.RemoteIpAddress?.ToString();
    }
    
    private static async Task RespondWithForbidden(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";
        
        var error = new
        {
            error = new
            {
                message = message,
                type = "ip_filter_error",
                code = StatusCodes.Status403Forbidden
            }
        };
        
        await JsonSerializer.SerializeAsync(context.Response.Body, error);
    }
}

/// <summary>
/// Extension methods for registering the IP filter middleware
/// </summary>
public static class IpFilterMiddlewareExtensions
{
    /// <summary>
    /// Adds the IP filter middleware to the application pipeline
    /// </summary>
    /// <param name="builder">The application builder</param>
    /// <returns>The application builder for chaining</returns>
    public static IApplicationBuilder UseIpFiltering(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<IpFilterMiddleware>();
    }
}