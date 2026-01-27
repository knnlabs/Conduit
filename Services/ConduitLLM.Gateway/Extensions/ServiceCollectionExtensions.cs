using ConduitLLM.Gateway.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
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

            // Register security service with factory to make distributed cache optional
            services.AddSingleton<ISecurityService>(serviceProvider =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<GatewaySecurityOptions>>();
                var config = serviceProvider.GetRequiredService<IConfiguration>();
                var logger = serviceProvider.GetRequiredService<ILogger<SecurityService>>();
                var memoryCache = serviceProvider.GetRequiredService<IMemoryCache>();
                
                return new SecurityService(options, config, logger, memoryCache, serviceProvider);
            });
            
            // Register IP filter service as scoped since it depends on scoped repository
            services.AddScoped<IIpFilterService, IpFilterService>();
            
            return services;
        }
    }
}