using System;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Configuration.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ConduitLLM.Configuration.Extensions
{
    /// <summary>
    /// Extension methods for registering provider health monitoring services
    /// </summary>
    public static class ProviderHealthServiceExtensions
    {
        /// <summary>
        /// Adds provider health monitoring services to the service collection
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">The configuration</param>
        /// <returns>The service collection</returns>
        public static IServiceCollection AddProviderHealthMonitoring(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Register the options
            services.Configure<ProviderHealthOptions>(
                configuration.GetSection(ProviderHealthOptions.SectionName));
            
            // Register the repository
            services.AddScoped<IProviderHealthRepository, ProviderHealthRepository>();
            
            return services;
        }
    }
}