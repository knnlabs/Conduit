using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Configuration.Services;
using ConduitLLM.Core;
using ConduitLLM.Core.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Routing;
using ConduitLLM.Core.Routing;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.WebUI.Extensions
{
    /// <summary>
    /// Extension methods for registering router services in the dependency injection container
    /// </summary>
    public static class RouterExtensions
    {
        /// <summary>
        /// Adds router services to the service collection
        /// </summary>
        /// <param name="services">The service collection to add services to</param>
        /// <param name="configuration">The configuration containing router settings</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddRouterServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Configure router options
            services.Configure<RouterOptions>(
                configuration.GetSection(RouterOptions.SectionName));

            // Register router services conditionally
            var routerOptions = new RouterOptions();
            configuration.GetSection(RouterOptions.SectionName).Bind(routerOptions);

            // First, make sure the Conduit registry is registered
            services.AddSingleton<ConduitRegistry>();

            // Register the client factory interface
            services.AddScoped<ILLMClientFactory, DefaultLLMClientFactory>();

            // Register router configuration repository
            // Check if using Admin API before registering the DB router config repository
            var useAdminApiStr = configuration["CONDUIT_USE_ADMIN_API"];
            bool useAdminApi = string.IsNullOrEmpty(useAdminApiStr) || useAdminApiStr.ToLowerInvariant() != "false";
            
            // RouterOptionsService has been removed in favor of using the Admin API

            // Register router service provider for management
            services.AddScoped<IRouterService, Services.Providers.RouterServiceProvider>();

            // Register the model health check service 
            // (it uses IOptionsMonitor<RouterOptions> instead of RouterOptionsService)
            services.AddHostedService<ModelHealthCheckService>();

            if (routerOptions.Enabled)
            {
                // Add router-specific services with DefaultLLMRouter
                services.AddScoped<ILLMRouter>(sp => 
                {
                    var clientFactory = sp.GetRequiredService<ILLMClientFactory>();
                    var logger = sp.GetRequiredService<ILogger<DefaultLLMRouter>>();
                    
                    // Create a default router with an initial configuration
                    var router = new DefaultLLMRouter(clientFactory, logger);
                    
                    // The RouterService will initialize the router with the proper configuration
                    // during its initialization phase
                    return router;
                });
                
                // Update Conduit registration to include the router
                services.AddScoped<Conduit>(sp => 
                {
                    var clientFactory = sp.GetRequiredService<ILLMClientFactory>();
                    var router = sp.GetRequiredService<ILLMRouter>();
                    var logger = sp.GetRequiredService<ILogger<Conduit>>();
                    var contextManager = sp.GetService<IContextManager>();
                    var modelProviderMappingService = sp.GetService<ConduitLLM.WebUI.Interfaces.IModelProviderMappingService>();
                    var contextOptions = sp.GetService<IOptions<ContextManagementOptions>>();
                    
                    return new Conduit(
                        clientFactory, 
                        logger,
                        router: router,
                        contextManager: contextManager,
                        contextOptions: contextOptions);
                });
            }
            else
            {
                // Add null router to ensure interface is satisfied when disabled
                services.AddScoped<ILLMRouter>(sp => null!);
                
                // Register default Conduit without router
                services.AddScoped<Conduit>(sp => 
                {
                    var clientFactory = sp.GetRequiredService<ILLMClientFactory>();
                    var logger = sp.GetRequiredService<ILogger<Conduit>>();
                    var contextManager = sp.GetService<IContextManager>();
                    var modelProviderMappingService = sp.GetService<ConduitLLM.WebUI.Interfaces.IModelProviderMappingService>();
                    var contextOptions = sp.GetService<IOptions<ContextManagementOptions>>();
                    
                    return new Conduit(
                        clientFactory,
                        logger,
                        contextManager: contextManager,
                        contextOptions: contextOptions);
                });
            }

            return services;
        }
    }
}
