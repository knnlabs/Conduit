using ConduitLLM.Core.Interfaces;
using ConduitLLM.Providers.Configuration;
using ConduitLLM.Providers.Http;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Providers.Extensions;

/// <summary>
/// Extension methods for registering LLM provider HttpClient instances with resilience policies.
/// </summary>
public static class HttpClientExtensions
{
    /// <summary>
    /// Adds HttpClient registration with retry policies for all LLM provider clients.
    /// </summary>
    /// <remarks>
    /// Registers the exact named clients that <c>BaseLLMClient.CreateHttpClient</c> requests at
    /// runtime (via <see cref="ProviderHttpClientNames"/>). Only the retry policy is attached:
    /// every provider client already enforces <c>HttpClient.Timeout</c> (120s), and a Polly
    /// timeout here would tighten that to 100s for long operations like image generation.
    /// Auth-verification and video named clients are deliberately not registered — auth checks
    /// should fail fast without retries, and video generation must never inherit an interactive
    /// timeout/retry budget.
    /// </remarks>
    /// <param name="services">The IServiceCollection to add services to</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddLLMProviderHttpClients(this IServiceCollection services)
    {
        // Configure retry options from configuration
        services.AddOptions<ProviderRetryOptions>()
            .BindConfiguration(ProviderRetryOptions.SectionName);

        foreach (var providerType in ProviderHttpClientNames.RegisteredTypes)
        {
            services.AddHttpClient(ProviderHttpClientNames.Chat(providerType))
                .AddProviderRetryPolicy();
        }

        return services;
    }

    /// <summary>
    /// Adds the retry resilience policy (with error tracking when available) to an HttpClient.
    /// Uses configuration from ProviderRetryOptions.
    /// </summary>
    /// <remarks>
    /// Deliberately does not attach a Polly timeout policy: provider clients set
    /// <c>HttpClient.Timeout</c> themselves (<c>BaseLLMClient.ConfigureHttpClient</c>), and that
    /// timeout spans the whole handler chain including retries.
    /// </remarks>
    /// <param name="builder">The HttpClient builder</param>
    /// <returns>The HttpClient builder for chaining</returns>
    public static IHttpClientBuilder AddProviderRetryPolicy(this IHttpClientBuilder builder)
    {
        return builder
            .AddPolicyHandler((provider, _) =>
            {
                var logger = provider.GetService<ILogger<ILLMClient>>();
                var retryOptions = provider.GetService<IOptions<ProviderRetryOptions>>()?.Value
                    ?? new ProviderRetryOptions();

                // Use error tracking retry policy if error tracking service is available
                var errorTracker = provider.GetService<IProviderErrorTrackingService>();
                if (errorTracker != null)
                {
                    return ResiliencePolicies.GetRetryPolicyWithErrorTracking(
                        provider,
                        retryOptions.MaxRetries,
                        TimeSpan.FromSeconds(retryOptions.InitialDelaySeconds),
                        TimeSpan.FromSeconds(retryOptions.MaxDelaySeconds));
                }

                // Fall back to standard retry policy if error tracking is not available
                return ResiliencePolicies.GetRetryPolicy(
                    retryOptions.MaxRetries,
                    TimeSpan.FromSeconds(retryOptions.InitialDelaySeconds),
                    TimeSpan.FromSeconds(retryOptions.MaxDelaySeconds),
                    retryOptions.EnableRetryLogging ? logger : null);
            });
    }
}
