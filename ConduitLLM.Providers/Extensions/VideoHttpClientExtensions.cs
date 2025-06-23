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
            // Note: MiniMaxClient creates its own HttpClient for video generation
            // to ensure no timeout policies are applied. This avoids conflicts with
            // the standard HTTP client registration that includes timeout policies.
            
            // When other providers add video support, they should follow the same pattern:
            // Create a new HttpClient instance in their video methods rather than using
            // the factory, to ensure complete control over timeout settings.

            return services;
        }
    }
}