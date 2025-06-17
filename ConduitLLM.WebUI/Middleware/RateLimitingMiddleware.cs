using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace ConduitLLM.WebUI.Middleware;

/// <summary>
/// Middleware that implements rate limiting based on IP addresses
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly RateLimitOptions _options;
    private readonly IDistributedCache? _distributedCache;

    // In-memory fallback for when Redis is not available
    private readonly ConcurrentDictionary<string, RateLimitInfo> _requestCounts = new();
    private DateTime _lastCleanup = DateTime.UtcNow;

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        IConfiguration configuration,
        IDistributedCache? distributedCache = null)
    {
        _next = next;
        _logger = logger;
        _distributedCache = distributedCache;
        _options = LoadOptionsFromConfiguration(configuration);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.Enabled)
        {
            await _next(context);
            return;
        }

        // Skip rate limiting for excluded paths
        var path = context.Request.Path.Value ?? "";
        if (IsExcludedPath(path))
        {
            await _next(context);
            return;
        }

        var clientIp = GetClientIpAddress(context);
        if (string.IsNullOrEmpty(clientIp))
        {
            _logger.LogWarning("Could not determine client IP for rate limiting");
            await _next(context);
            return;
        }

        // Check rate limit
        if (await IsRateLimitExceededAsync(clientIp))
        {
            _logger.LogWarning("Rate limit exceeded for IP: {IpAddress}", clientIp);
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.Append("Retry-After", _options.WindowSeconds.ToString());
            
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = new
                {
                    message = "Rate limit exceeded. Please try again later.",
                    type = "rate_limit_error",
                    code = StatusCodes.Status429TooManyRequests
                }
            }));
            
            return;
        }

        await _next(context);
    }

    private async Task<bool> IsRateLimitExceededAsync(string clientIp)
    {
        var key = $"conduit:ratelimit:{clientIp}";
        var now = DateTime.UtcNow;

        // Try distributed cache first
        if (_distributedCache != null)
        {
            try
            {
                var data = await _distributedCache.GetStringAsync(key);
                RateLimitInfo? info = null;

                if (!string.IsNullOrEmpty(data))
                {
                    info = JsonSerializer.Deserialize<RateLimitInfo>(data);
                }

                if (info == null || (now - info.WindowStart).TotalSeconds >= _options.WindowSeconds)
                {
                    // New window
                    info = new RateLimitInfo
                    {
                        WindowStart = now,
                        RequestCount = 1
                    };
                }
                else
                {
                    info.RequestCount++;
                }

                // Save updated info
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_options.WindowSeconds)
                };
                await _distributedCache.SetStringAsync(key, JsonSerializer.Serialize(info), options);

                return info.RequestCount > _options.MaxRequests;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accessing distributed cache for rate limiting");
                // Fall through to in-memory implementation
            }
        }

        // In-memory fallback
        CleanupOldEntriesIfNeeded();

        var rateInfo = _requestCounts.AddOrUpdate(clientIp,
            new RateLimitInfo { WindowStart = now, RequestCount = 1 },
            (key, existing) =>
            {
                if ((now - existing.WindowStart).TotalSeconds >= _options.WindowSeconds)
                {
                    // New window
                    return new RateLimitInfo { WindowStart = now, RequestCount = 1 };
                }
                else
                {
                    // Same window, increment
                    existing.RequestCount++;
                    return existing;
                }
            });

        return rateInfo.RequestCount > _options.MaxRequests;
    }

    private void CleanupOldEntriesIfNeeded()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastCleanup).TotalMinutes < 5)
            return;

        _lastCleanup = now;

        // Remove entries older than the window
        var cutoff = now.AddSeconds(-_options.WindowSeconds);
        var keysToRemove = _requestCounts
            .Where(kvp => kvp.Value.WindowStart < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _requestCounts.TryRemove(key, out _);
        }
    }

    private bool IsExcludedPath(string path)
    {
        return _options.ExcludedPaths.Any(excluded => 
            path.StartsWith(excluded, StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetClientIpAddress(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }

    private RateLimitOptions LoadOptionsFromConfiguration(IConfiguration configuration)
    {
        var options = new RateLimitOptions();

        options.Enabled = configuration.GetValue<bool>("CONDUIT_RATE_LIMITING_ENABLED", false);
        options.MaxRequests = configuration.GetValue<int>("CONDUIT_RATE_LIMIT_MAX_REQUESTS", 100);
        options.WindowSeconds = configuration.GetValue<int>("CONDUIT_RATE_LIMIT_WINDOW_SECONDS", 60);

        var excludedPaths = configuration["CONDUIT_RATE_LIMIT_EXCLUDED_PATHS"];
        if (!string.IsNullOrWhiteSpace(excludedPaths))
        {
            options.ExcludedPaths = excludedPaths.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .ToList();
        }

        _logger.LogInformation("Rate limiting configured: {Enabled}, Max: {MaxRequests}/{WindowSeconds}s",
            options.Enabled, options.MaxRequests, options.WindowSeconds);

        return options;
    }

    private class RateLimitOptions
    {
        public bool Enabled { get; set; } = false;
        public int MaxRequests { get; set; } = 100;
        public int WindowSeconds { get; set; } = 60;
        public List<string> ExcludedPaths { get; set; } = new()
        {
            "/health",
            "/_blazor",
            "/css",
            "/js",
            "/images",
            "/favicon.ico"
        };
    }

    private class RateLimitInfo
    {
        public DateTime WindowStart { get; set; }
        public int RequestCount { get; set; }
    }
}

/// <summary>
/// Extension methods for rate limiting middleware
/// </summary>
public static class RateLimitingMiddlewareExtensions
{
    /// <summary>
    /// Adds rate limiting middleware to the application pipeline
    /// </summary>
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RateLimitingMiddleware>();
    }
}