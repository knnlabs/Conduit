using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ConduitLLM.Http.Services;

namespace ConduitLLM.Http.Extensions
{
    /// <summary>
    /// Extension methods for service registration
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds Core API security services to the service collection
        /// </summary>
        public static IServiceCollection AddCoreApiSecurity(this IServiceCollection services, IConfiguration configuration)
        {
            // Configure security options from environment variables
            services.ConfigureCoreApiSecurityOptions(configuration);
            
            // Register security service
            services.AddSingleton<ISecurityService, SecurityService>();
            
            // Register IP filter service
            services.AddScoped<IIpFilterService, IpFilterService>();
            
            return services;
        }
    }
}