using ConduitLLM.Gateway.Interfaces;
using ConduitLLM.Gateway.Services;
using ConduitLLM.Security.Options;

namespace ConduitLLM.Gateway.Extensions
{
    /// <summary>
    /// Extension methods for service registration
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds Gateway API security services to the service collection
        /// </summary>
        public static IServiceCollection AddCoreApiSecurity(this IServiceCollection services, IConfiguration configuration)
        {
            // Configure security options from environment variables
            services.ConfigureCoreApiSecurityOptions(configuration);

            // Note: Distributed cache should be registered in Program.cs before calling this method
            // to ensure proper Redis configuration for production environments

            // Register security service (all deps resolved by DI, including IServiceProvider)
            services.AddSingleton<ISecurityService, SecurityService>();
            
            // Register IP filter service as scoped since it depends on scoped repository
            services.AddScoped<IIpFilterService, IpFilterService>();
            
            return services;
        }
    }
}