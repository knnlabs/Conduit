using System.Diagnostics;
using System.Text;
using System.Text.Json;

using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Services;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Middleware;

/// <summary>
/// Middleware to track LLM requests, calculate token usage and update virtual key spending
/// </summary>
public class LlmRequestTrackingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LlmRequestTrackingMiddleware> _logger;
    private readonly IServiceProvider _serviceProvider;

    public LlmRequestTrackingMiddleware(
        RequestDelegate next,
        ILogger<LlmRequestTrackingMiddleware> logger,
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
}

// Extension method for easy registration in Program.cs
public static class LlmRequestTrackingMiddlewareExtensions
{
    public static IApplicationBuilder UseLlmRequestTracking(
        this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<LlmRequestTrackingMiddleware>();
    }
}
