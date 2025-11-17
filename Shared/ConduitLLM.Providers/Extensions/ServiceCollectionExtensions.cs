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
            services.AddScoped<ILLMClientFactory, DatabaseAwareLLMClientFactory>();

            // OBSOLETE: External model discovery is no longer used. 
            // The ProviderModelsController now returns models from the local database.
            // services.AddScoped<ModelListService>();

            // Ensure memory cache is registered
            services.AddMemoryCache();

            return services;
        }
    }
}
