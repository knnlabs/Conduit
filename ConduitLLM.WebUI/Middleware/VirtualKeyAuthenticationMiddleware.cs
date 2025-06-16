using System.Text;
using System.Text.Json;

using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Services;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Middleware;

/// <summary>
/// Middleware to handle virtual key authentication for API requests
/// </summary>
public class VirtualKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<VirtualKeyAuthenticationMiddleware> _logger;
    private readonly IServiceProvider _serviceProvider;

    // Headers that might contain the API key
    private const string AuthorizationHeader = "Authorization";
    private const string ApiKeyHeader = "X-Api-Key";
    private const string BearerPrefix = "Bearer ";

    public VirtualKeyAuthenticationMiddleware(
        RequestDelegate next,
        ILogger<VirtualKeyAuthenticationMiddleware> logger,
        IServiceProvider serviceProvider)
    {
        _next = next;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IVirtualKeyService virtualKeyService)
    {
        // API endpoints were moved to ConduitLLM.Http project
        // This middleware is now deprecated and just passes through
        await _next(context);
    }

    // Kept for reference but not used anymore
    private bool IsExcludedPath(PathString path)
    {
        // Example: exclude health check endpoint
        return path.StartsWithSegments("/v1/health");
    }
}

// Extension method for easy registration in Program.cs
public static class VirtualKeyAuthenticationMiddlewareExtensions
{
    public static IApplicationBuilder UseVirtualKeyAuthentication(
        this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<VirtualKeyAuthenticationMiddleware>();
    }
}
