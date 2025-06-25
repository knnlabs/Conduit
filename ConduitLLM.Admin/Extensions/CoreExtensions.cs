using ConduitLLM.Admin.Adapters;
using ConduitLLM.Core.Data;
using ConduitLLM.Core.Data.Extensions;
using ConduitLLM.Core.Extensions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
            // Add database services - use ConfigurationDbContext
            services.AddDatabaseServices<ConduitLLM.Configuration.ConfigurationDbContext>();

            // Register DbContext Factory (using connection string from environment variables)
            var connectionStringManager = new ConnectionStringManager();
            // Pass "AdminAPI" to get Admin API-specific connection pool settings
            var (dbProvider, dbConnectionString) = connectionStringManager.GetProviderAndConnectionString("AdminAPI", msg => Console.WriteLine(msg));

            if (dbProvider == "sqlite")
            {
                services.AddDbContextFactory<ConduitLLM.Configuration.ConfigurationDbContext>(options =>
                {
                    options.UseSqlite(dbConnectionString);
                    // Suppress PendingModelChangesWarning in production
                    var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
                    if (environment == "Production")
                    {
                        options.ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
                    }
                });
            }
            else if (dbProvider == "postgres")
            {
                services.AddDbContextFactory<ConduitLLM.Configuration.ConfigurationDbContext>(options =>
                {
                    options.UseNpgsql(dbConnectionString);
                    // Suppress PendingModelChangesWarning in production
                    var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
                    if (environment == "Production")
                    {
                        options.ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
                    }
                });
            }
            else
            {
                throw new InvalidOperationException($"Unsupported database provider: {dbProvider}. Supported values are 'sqlite' and 'postgres'.");
            }

            // Add context management services
            services.AddConduitContextManagement(configuration);

            // Add Configuration adapters (moved from Core)
            services.AddConfigurationAdapters();

            return services;
        }
    }
}
