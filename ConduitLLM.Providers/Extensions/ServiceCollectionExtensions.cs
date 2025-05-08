using System;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ConduitLLM.Providers.Extensions
{
    /// <summary>
    /// Extension methods for configuring provider services with dependency injection.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds provider services to the service collection.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddProviderServices(this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }
            
            // Register LLM client factory
            services.AddScoped<ILLMClientFactory, LLMClientFactory>();
            
            // Register model list service
            services.AddScoped<ModelListService>();
            
            // Ensure memory cache is registered
            services.AddMemoryCache();
            
            return services;
        }
    }
}