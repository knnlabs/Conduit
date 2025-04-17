using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Http.Security
{
    public class VirtualKeyRateLimitPolicy : IRateLimiterPolicy<HttpContext>
    {
        private readonly IVirtualKeyService _virtualKeyService;

        public VirtualKeyRateLimitPolicy(IVirtualKeyService virtualKeyService)
        {
            _virtualKeyService = virtualKeyService;
        }

        public RateLimitPartition<HttpContext> GetPartition(HttpContext httpContext)
        {
            string? originalApiKey = null;
            if (httpContext.Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                var authValue = authHeader.FirstOrDefault();
                if (!string.IsNullOrEmpty(authValue) && authValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    originalApiKey = authValue.Substring("Bearer ".Length).Trim();
                }
            }
            else if (httpContext.Request.Headers.TryGetValue("api-key", out var apiKeyHeader))
            {
                originalApiKey = apiKeyHeader.FirstOrDefault();
            }

            if (originalApiKey?.StartsWith("condt_", StringComparison.OrdinalIgnoreCase) == true)
            {
                // NOTE: This is a synchronous interface! If you need to do async DB work, you must cache limits elsewhere or prefetch.
                // For now, just return no limiter for virtual keys (or a default global limiter if desired).
                // See docs: https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.ratelimiting.iratelimiterpolicy-1.getpartition?view=aspnetcore-8.0
                return RateLimitPartition.GetNoLimiter<HttpContext>(httpContext);
            }
            return RateLimitPartition.GetNoLimiter<HttpContext>(httpContext);
        }

        public Func<HttpContext, RateLimitPartition<HttpContext>>? GetPartitionKeyProvider() => null;

        public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => (context, token) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            return ValueTask.CompletedTask;
        };
    }
}
