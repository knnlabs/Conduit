using ConduitLLM.Core.Data.Extensions;
using ConduitLLM.Core.Extensions;
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
            // Add database services - use DbContext type directly
            services.AddDatabaseServices<Microsoft.EntityFrameworkCore.DbContext>();
            
            // Add context management services
            services.AddConduitContextManagement(configuration);
            
            return services;
        }
    }
}