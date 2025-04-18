using ConduitLLM.Core.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ConduitLLM.Core.Extensions
{
    /// <summary>
    /// Extension methods for configuring ConduitLLM Core services in an IServiceCollection.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the ConduitLLM Context Window Management services to the service collection.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <param name="configuration">The configuration instance.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddConduitContextManagement(this IServiceCollection services, IConfiguration configuration)
        {
            // Register configuration options
            services.Configure<ContextManagementOptions>(
                configuration.GetSection("ConduitLLM:ContextManagement"));

            // Register token counter
            services.AddSingleton<ITokenCounter, TiktokenCounter>();

            // Register context manager
            services.AddScoped<IContextManager, ContextManager>();

            return services;
        }
    }
}
