using System.Text.Json;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using ConduitLLM.Gateway.Metrics;

namespace ConduitLLM.Gateway.Middleware
{
    /// <summary>
    /// Enforces per-virtual-key sliding-window RPM/RPD rate limits via
    /// <see cref="IVirtualKeyRateLimitService"/>. The authenticated key's hash and
    /// configured limits are stashed in <c>HttpContext.Items</c> by
    /// <c>VirtualKeyAuthenticationHandler</c>, so this middleware does no DB lookups.
    /// </summary>
    /// <remarks>
    /// Behavior contract:
    /// - Excluded paths (health, metrics, public media, SignalR hubs) are passed through.
    /// - Requests not authenticated via the VirtualKey scheme (e.g., Backend service-to-service)
    ///   are passed through — they have no <c>VirtualKey.KeyHash</c> in <c>HttpContext.Items</c>.
    /// - Keys with <c>RateLimitRpm == null</c> AND <c>RateLimitRpd == null</c> are unlimited.
    /// - On rate-limit rejection, returns 429 with <c>Retry-After</c> and an OpenAI-shaped error body.
    /// - On any internal exception (Redis unavailable, etc.), logs a warning and lets the request
    ///   through (fail open) — preferable to tanking the gateway.
    /// </remarks>
    public class VirtualKeyRateLimitMiddleware
    {
        private static readonly string[] ExcludedPathPrefixes =
        {
            "/health",
            "/metrics",
            "/v1/conduit/media/public",
            "/hubs"
        };

        private readonly RequestDelegate _next;
        private readonly IVirtualKeyRateLimitService _rateLimitService;
        private readonly ILogger<VirtualKeyRateLimitMiddleware> _logger;

        public VirtualKeyRateLimitMiddleware(
            RequestDelegate next,
            IVirtualKeyRateLimitService rateLimitService,
            ILogger<VirtualKeyRateLimitMiddleware> logger)
        {
            _next = next;
            _rateLimitService = rateLimitService;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (ShouldSkip(context))
            {
                await _next(context);
                return;
            }

            // Only authenticated VirtualKey-scheme requests carry a KeyHash in Items.
            // Backend-scheme and unauthenticated requests pass through unrestricted here.
            if (context.Items["VirtualKey.KeyHash"] is not string keyHash || string.IsNullOrEmpty(keyHash))
            {
                await _next(context);
                return;
            }

            var rpmLimit = context.Items["VirtualKey.RateLimitRpm"] as int?;
            var rpdLimit = context.Items["VirtualKey.RateLimitRpd"] as int?;

            // Null/zero on both = unlimited; skip the Redis round-trip.
            if (!HasConfiguredLimit(rpmLimit) && !HasConfiguredLimit(rpdLimit))
            {
                await _next(context);
                return;
            }

            RateLimitCheckResult? result = null;
            try
            {
                result = await _rateLimitService.CheckRateLimitAsync(keyHash, rpmLimit, rpdLimit);
            }
            catch (Exception ex)
            {
                // Fail open: rate limiting is defense-in-depth; never tank the request path.
                _logger.LogWarning(ex, "Rate limit check failed for virtual key {KeyHashPrefix}; allowing request",
                    SafeKeyPrefix(keyHash));
                GatewayRateLimitMetrics.RecordError();
                await _next(context);
                return;
            }

            SetRateLimitHeaders(context, result);

            if (!result.IsAllowed)
            {
                GatewayRateLimitMetrics.RecordRejected(result.LimitType);
                await WriteRateLimitedResponseAsync(context, result);
                return;
            }

            GatewayRateLimitMetrics.RecordAllowed(result.LimitType);
            await _next(context);
        }

        private static bool ShouldSkip(HttpContext context)
        {
            var path = context.Request.Path.Value;
            if (string.IsNullOrEmpty(path)) return false;
            foreach (var prefix in ExcludedPathPrefixes)
            {
                if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static bool HasConfiguredLimit(int? limit) => limit.HasValue && limit.Value > 0;

        private static void SetRateLimitHeaders(HttpContext context, RateLimitCheckResult result)
        {
            // Headers are informational — set on both allow and deny so clients can pace themselves.
            context.Response.Headers["X-RateLimit-Limit"] = result.Limit.ToString();
            context.Response.Headers["X-RateLimit-Remaining"] = result.RequestsRemaining.ToString();
            context.Response.Headers["X-RateLimit-Reset"] = ResetUnixSeconds(result.ResetsAt).ToString();
            if (!string.IsNullOrEmpty(result.LimitType))
            {
                context.Response.Headers["X-RateLimit-Scope"] = result.LimitType;
            }
        }

        /// <summary>
        /// Rounds the reset instant up to the next whole second. Truncating would advertise a
        /// moment fractionally before the window actually frees, so a client retrying exactly
        /// on the hint would be rejected again.
        /// </summary>
        private static long ResetUnixSeconds(DateTime resetsAt)
        {
            var ms = new DateTimeOffset(DateTime.SpecifyKind(resetsAt, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
            return (ms + 999) / 1000;
        }

        private static async Task WriteRateLimitedResponseAsync(HttpContext context, RateLimitCheckResult result)
        {
            // Ceiling, not truncation — see ResetUnixSeconds. A 4.2s wait must be advertised as 5.
            var retryAfterSeconds = Math.Max(1, (int)Math.Ceiling((result.ResetsAt - DateTime.UtcNow).TotalSeconds));
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers["Retry-After"] = retryAfterSeconds.ToString();
            context.Response.ContentType = "application/json";

            var error = new OpenAIErrorResponse
            {
                Error = new OpenAIError
                {
                    Message = $"{result.LimitType} rate limit exceeded ({result.Limit} requests). Retry after {retryAfterSeconds} seconds.",
                    Type = "rate_limit_exceeded",
                    Code = "rate_limit_exceeded"
                }
            };

            await JsonSerializer.SerializeAsync(context.Response.Body, error);
        }

        private static string SafeKeyPrefix(string keyHash)
        {
            return keyHash.Length <= 8 ? keyHash : keyHash[..8];
        }
    }

    public static class VirtualKeyRateLimitMiddlewareExtensions
    {
        public static IApplicationBuilder UseVirtualKeyRateLimiting(this IApplicationBuilder app)
        {
            return app.UseMiddleware<VirtualKeyRateLimitMiddleware>();
        }
    }
}
