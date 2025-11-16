using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Configuration.Interfaces;

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

            // Register model provider mapping service with caching decorator pattern
            services.AddScoped<ModelProviderMappingService>(); // Inner service
            services.AddScoped<IModelProviderMappingService>(provider =>
            {
                var innerService = provider.GetRequiredService<ModelProviderMappingService>();
                var cacheManager = provider.GetRequiredService<ConduitLLM.Core.Interfaces.ICacheManager>();
                var logger = provider.GetRequiredService<ILogger<CachedModelProviderMappingService>>();
                return new CachedModelProviderMappingService(innerService, cacheManager, logger);
            });

            return services;
        }
    }
}
