using ConduitLLM.Core.Data;
using ConduitLLM.Core.Data.Extensions;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;

using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Admin.Extensions
{
    /// <summary>
    /// Extension methods for configuring Core services in the Admin API
    /// </summary>
    public static class CoreExtensions
    {
        /// <summary>
        /// Adds the Core services to the DI container
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">The application configuration</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddCoreServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Register unified cache manager (required by CacheManagementService)
            services.AddCacheManager(configuration);

            // Add database services - use ConfigurationDbContext
            services.AddDatabaseServices<ConduitLLM.Configuration.ConduitDbContext>();

            // Register DbContext Factory (using connection string from environment variables)
            var connectionStringManager = new ConnectionStringManager();
            // Pass "AdminAPI" to get Admin API-specific connection pool settings
            var (dbProvider, dbConnectionString) = connectionStringManager.GetProviderAndConnectionString("AdminAPI", msg => Console.WriteLine(msg));
            
            // Log the connection pool settings for verification
            if (dbProvider == "postgres" && dbConnectionString.Contains("MaxPoolSize"))
            {
                Console.WriteLine($"[ConduitLLM.Admin] Admin API database connection pool configured:");
                var match = System.Text.RegularExpressions.Regex.Match(dbConnectionString, @"MinPoolSize=(\d+);MaxPoolSize=(\d+)");
                if (match.Success)
                {
                    Console.WriteLine($"[ConduitLLM.Admin]   Min Pool Size: {match.Groups[1].Value}");
                    Console.WriteLine($"[ConduitLLM.Admin]   Max Pool Size: {match.Groups[2].Value}");
                }
            }

            // Only PostgreSQL is supported
            if (dbProvider != "postgres")
            {
                throw new InvalidOperationException($"Only PostgreSQL is supported. Invalid provider: {dbProvider}");
            }

            services.AddDbContextFactory<ConduitLLM.Configuration.ConduitDbContext>(options =>
            {
                options.UseNpgsql(dbConnectionString);
            });
            
            // Also add scoped registration from factory for services that need direct injection
            // Note: This creates contexts from the factory on demand
            services.AddScoped<ConduitLLM.Configuration.ConduitDbContext>(provider =>
            {
                var factory = provider.GetService<IDbContextFactory<ConduitLLM.Configuration.ConduitDbContext>>();
                if (factory == null)
                {
                    throw new InvalidOperationException("IDbContextFactory<ConfigurationDbContext> is not registered");
                }
                return factory.CreateDbContext();
            });

            // Add context management services
            services.AddConduitContextManagement(configuration);

            // Note: AddDiscoveryCache is not registered in Admin API as it's optional
            // SystemInfoController has IDiscoveryCacheService? as nullable dependency
            // If needed in the future, must first register AddCacheManager(configuration)

            // Add Function Discovery Cache for function tool definition caching
            services.AddFunctionDiscoveryCache(configuration);
            Console.WriteLine("[ConduitLLM.Admin] Function Discovery Cache registered - function tool definitions will be cached based on per-function TTL");

            // Add Provider Registry - single source of truth for provider metadata
            services.AddSingleton<IProviderMetadataRegistry, ProviderMetadataRegistry>();
            Console.WriteLine("[ConduitLLM.Admin] Provider Registry registered - centralized provider metadata management enabled");

            return services;
        }
    }
}
