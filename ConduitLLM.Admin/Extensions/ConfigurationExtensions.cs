using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ConduitLLM.Admin.Extensions
{
    /// <summary>
    /// Extension methods for configuring Configuration services in the Admin API
    /// </summary>
    public static class ConfigurationExtensions
    {
        /// <summary>
        /// Adds the Configuration services to the DI container
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">The application configuration</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddConfigurationServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Add repositories
            services.AddRepositories();

            // Add caching services
            services.AddCachingServices(configuration);

            // Add database initialization
            services.AddDatabaseInitialization();
            
            // Add Configuration services
            services.AddScoped<IProviderService, ProviderService>();
            services.AddScoped<IModelProviderMappingService, ModelProviderMappingService>();

            return services;
        }
    }
}
