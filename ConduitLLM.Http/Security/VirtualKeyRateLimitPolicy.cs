using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace ConduitLLM.Http.Security
{
    /// <summary>
    /// Implements a rate limiting policy for virtual keys in the Conduit API.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This policy applies rate limits to API requests based on the virtual key
    /// used in the request. This helps prevent abuse and ensures fair resource
    /// allocation among different clients.
    /// </para>
    /// <para>
    /// The policy extracts the API key from either the "Authorization" header (as a Bearer token)
    /// or from the "api-key" header. It identifies virtual keys by their "condt_" prefix.
    /// </para>
    /// <para>
    /// Currently, this implementation does not apply rate limits to virtual keys,
    /// returning a "no limiter" partition for all requests. This is because the
    /// <see cref="IRateLimiterPolicy{T}.GetPartition"/> method must be synchronous,
    /// while retrieving virtual key limits from the database requires async operations.
    /// </para>
    /// <para>
    /// Future implementations could use a cached service to prefetch and maintain
    /// rate limit configurations for virtual keys, enabling per-key rate limiting.
    /// </para>
    /// </remarks>
    public class VirtualKeyRateLimitPolicy : IRateLimiterPolicy<HttpContext>
    {
        private readonly IVirtualKeyService _virtualKeyService;

        /// <summary>
        /// Initializes a new instance of the <see cref="VirtualKeyRateLimitPolicy"/> class.
        /// </summary>
        /// <param name="virtualKeyService">The service for managing virtual keys.</param>
        public VirtualKeyRateLimitPolicy(IVirtualKeyService virtualKeyService)
        {
            _virtualKeyService = virtualKeyService;
        }

        /// <summary>
        /// Gets the rate limit partition for the given HTTP context.
        /// </summary>
        /// <param name="httpContext">The HTTP context of the current request.</param>
        /// <returns>A rate limit partition for the request.</returns>
        /// <remarks>
        /// This method:
        /// <list type="bullet">
        /// <item>Extracts the API key from the request headers</item>
        /// <item>Identifies virtual keys by their "condt_" prefix</item>
        /// <item>Currently returns a "no limiter" partition for all requests</item>
        /// </list>
        /// Note that this method must be synchronous as required by the ASP.NET Core rate limiting infrastructure,
        /// which limits our ability to look up dynamic rate limits from the database.
        /// </remarks>
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

        /// <summary>
        /// Gets a function that provides the partition key for the given HTTP context.
        /// </summary>
        /// <returns>
        /// Null, as this policy uses the <see cref="GetPartition"/> method instead.
        /// </returns>
        /// <remarks>
        /// This implementation returns null because it uses the <see cref="GetPartition"/> method
        /// to determine the rate limit partition directly, rather than using a partition key.
        /// </remarks>
        public Func<HttpContext, RateLimitPartition<HttpContext>>? GetPartitionKeyProvider() => null;

        /// <summary>
        /// Gets a function that is called when a request is rejected due to rate limiting.
        /// </summary>
        /// <remarks>
        /// When a request is rejected, this function:
        /// <list type="bullet">
        /// <item>Sets the HTTP status code to 429 Too Many Requests</item>
        /// <item>Returns a completed ValueTask</item>
        /// </list>
        /// </remarks>
        public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => (context, token) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            return ValueTask.CompletedTask;
        };
    }
}
