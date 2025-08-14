using System.Collections.Generic;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using Microsoft.Extensions.DependencyInjection;
using ConduitLLM.Configuration.Interfaces;

namespace ConduitLLM.Http.Adapters
{
    /// <summary>
    /// Provides adapters that map Configuration services to Core interfaces for the Http API.
    /// </summary>
    internal static class ConfigurationAdapters
    {
        /// <summary>
        /// Registers Core configuration service interfaces with their Configuration implementations.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddConfigurationAdapters(this IServiceCollection services)
        {
            // Note: Adapters removed as the interfaces they were adapting to no longer exist
            // The services are registered directly in their respective service registrations
            return services;
        }
    }
}
