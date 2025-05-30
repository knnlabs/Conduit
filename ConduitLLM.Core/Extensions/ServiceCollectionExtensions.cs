using ConduitLLM.Core.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using ConduitLLM.Core.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

        /// <summary>
        /// Adds the ConduitLLM Audio services to the service collection.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddConduitAudioServices(this IServiceCollection services)
        {
            // Register audio router
            services.AddScoped<IAudioRouter, DefaultAudioRouter>();

            // Register capability detector if not already registered
            services.TryAddScoped<IModelCapabilityDetector, ModelCapabilityDetector>();

            return services;
        }
    }
}
