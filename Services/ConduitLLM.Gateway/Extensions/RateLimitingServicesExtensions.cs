using ConduitLLM.Configuration.Options;
using ConduitLLM.Core.Services;
using ConduitLLM.Gateway.RateLimiting;

using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace ConduitLLM.Gateway.Extensions;

/// <summary>
/// Registers the rate-limiting primitives shared by the middleware and the endpoint filters.
/// </summary>
public static class RateLimitingServicesExtensions
{
    public static IServiceCollection AddConduitRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.SectionName));
        services.PostConfigure<RateLimitOptions>(ApplyFailureModeEnvironmentOverride);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<RateLimitOptions>>().Value);
        services.AddSingleton<IRateLimitFailurePolicy, RateLimitFailurePolicy>();

        services.AddSingleton<RequestTokenEstimator>();
        services.AddSingleton<TokenRateLimitFilter>();

        // Distributed limits need Redis. Where it is absent the endpoint filter still has to
        // resolve, so bind an implementation that admits everything rather than letting every
        // request to a token-limited route fail on a missing dependency.
        var redisConnectionString = ConduitLLM.Configuration.Utilities.RedisUrlParser.ResolveConnectionString();
        if (string.IsNullOrEmpty(redisConnectionString))
        {
            services.AddSingleton<ITokenRateLimitService, UnlimitedTokenRateLimitService>();
            services.AddSingleton<IConcurrencyRateLimitService, UnlimitedConcurrencyRateLimitService>();
            return services;
        }

        // The window primitive is shared by every scope (key, group, per-model) so they can be
        // evaluated together in one atomic call.
        services.AddSingleton<ISlidingWindowRateLimiter>(sp => new SlidingWindowRateLimiter(
            sp.GetRequiredService<IConnectionMultiplexer>(),
            sp.GetRequiredService<ILogger<SlidingWindowRateLimiter>>()));

        services.AddSingleton<ITokenRateLimitService, TokenRateLimitService>();
        services.AddSingleton<IConcurrencyRateLimitService, ConcurrencyRateLimitService>();

        return services;
    }

    /// <summary>
    /// Honours CONDUIT_RATE_LIMIT_FAILURE_MODE, the documented name for this switch.
    /// </summary>
    /// <remarks>
    /// An unrecognised value keeps the default rather than failing boot: a typo in an
    /// operational switch should not stop the gateway starting, but it must not silently be
    /// read as the stricter setting either.
    /// </remarks>
    private static void ApplyFailureModeEnvironmentOverride(RateLimitOptions options)
    {
        var configured = Environment.GetEnvironmentVariable("CONDUIT_RATE_LIMIT_FAILURE_MODE");
        if (string.IsNullOrWhiteSpace(configured))
        {
            return;
        }

        if (Enum.TryParse<RateLimitFailureMode>(configured.Trim(), ignoreCase: true, out var mode))
        {
            options.FailureMode = mode;
        }
    }
}
