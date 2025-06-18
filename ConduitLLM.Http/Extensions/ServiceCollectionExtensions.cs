using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ConduitLLM.Http.Services;
using ConduitLLM.Http.Options;

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
            
            // Register distributed cache (in-memory for tests, Redis in production)
            // Always add this to ensure it's available for SecurityService
            services.AddDistributedMemoryCache();
            
            // Register security service with factory to make distributed cache optional
            services.AddSingleton<ISecurityService>(serviceProvider =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<SecurityOptions>>();
                var config = serviceProvider.GetRequiredService<IConfiguration>();
                var logger = serviceProvider.GetRequiredService<ILogger<SecurityService>>();
                var memoryCache = serviceProvider.GetRequiredService<IMemoryCache>();
                var ipFilterService = serviceProvider.GetRequiredService<IIpFilterService>();
                
                return new SecurityService(options, config, logger, memoryCache, ipFilterService, serviceProvider);
            });
            
            // Register IP filter service
            services.AddScoped<IIpFilterService, IpFilterService>();
            
            return services;
        }
    }
}