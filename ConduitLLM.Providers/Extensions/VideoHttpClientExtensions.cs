using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Extensions
{
    /// <summary>
    /// Extension methods for registering HTTP clients for video generation providers.
    /// These clients have no timeout policy to support long-running video generation.
    /// </summary>
    public static class VideoHttpClientExtensions
    {
        /// <summary>
        /// Adds HttpClient registration for video generation providers without timeout policies.
        /// Video generation can take 3-5+ minutes, so timeouts are not appropriate.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddVideoGenerationHttpClients(this IServiceCollection services)
        {
            // Register MiniMaxClient for video generation without timeout
            services.AddHttpClient("minimaxLLMClient", client =>
            {
                // Set a very long timeout at the HttpClient level (1 hour)
                client.Timeout = TimeSpan.FromHours(1);
            })
            .AddPolicyHandler((provider, _) =>
            {
                var logger = provider.GetService<ILogger<MiniMaxClient>>();
                // Use retry policy but NO timeout policy
                return ResiliencePolicies.GetRetryPolicy(
                    maxRetries: 3,
                    initialDelay: TimeSpan.FromSeconds(1),
                    maxDelay: TimeSpan.FromSeconds(30),
                    logger: logger);
            });

            // Add other video providers here as needed
            // Example: services.AddHttpClient("replicateLLMClient", ...)

            return services;
        }
    }
}