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
            services.ConfigureGatewaySecurityOptions(configuration);

            // Note: Distributed cache should be registered in Program.cs before calling this method
            // to ensure proper Redis configuration for production environments

            // Register security service for both shared and gateway-specific interfaces
            services.AddSingleton<SecurityService>();
            services.AddSingleton<ConduitLLM.Security.Interfaces.ISecurityService>(sp => sp.GetRequiredService<SecurityService>());
            services.AddSingleton<IGatewaySecurityService>(sp => sp.GetRequiredService<SecurityService>());
            
            // Register IP filter service as scoped since it depends on scoped repository
            services.AddScoped<IIpFilterService, IpFilterService>();
            
            return services;
        }
    }
}
