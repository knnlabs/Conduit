using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Options;
using ConduitLLM.WebUI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;

namespace ConduitLLM.WebUI.Extensions
{
    /// <summary>
    /// Extension methods for registering Admin API client services.
    /// </summary>
    public static class AdminClientExtensions
    {
        /// <summary>
        /// Adds the Admin API client to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The configuration.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddAdminApiClient(this IServiceCollection services, IConfiguration configuration)
        {
            // Configure Admin API options
            services.Configure<AdminApiOptions>(options =>
            {
                var section = configuration.GetSection("AdminApi");
                section.Bind(options);

                // Allow environment variable override
                var baseUrl = configuration["CONDUIT_ADMIN_API_URL"];
                if (!string.IsNullOrEmpty(baseUrl))
                {
                    options.BaseUrl = baseUrl;
                }

                var masterKey = configuration["CONDUIT_MASTER_KEY"];
                if (!string.IsNullOrEmpty(masterKey))
                {
                    options.MasterKey = masterKey;
                }

                var useAdminApi = configuration["CONDUIT_USE_ADMIN_API"];
                if (!string.IsNullOrEmpty(useAdminApi) && bool.TryParse(useAdminApi, out var useAdminApiValue))
                {
                    options.UseAdminApi = useAdminApiValue;
                }
            });

            // Register HTTP client
            services.AddHttpClient<IAdminApiClient, AdminApiClient>();

            return services;
        }

        /// <summary>
        /// Adds Admin API service adapters to the service collection.
        /// All services now use their adapter implementations via the Admin API.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The configuration.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddAdminApiAdapters(this IServiceCollection services, IConfiguration configuration)
        {

            // Register all adapter services
            services.AddScoped<Interfaces.IGlobalSettingService, Services.Adapters.GlobalSettingServiceAdapter>();
            services.AddScoped<Interfaces.IProviderCredentialService, Services.Adapters.ProviderCredentialServiceAdapter>();
            services.AddScoped<Interfaces.IModelCostService, Services.Adapters.ModelCostServiceAdapter>();
            services.AddScoped<ConduitLLM.Configuration.Services.IModelCostService>(sp => 
                sp.GetRequiredService<Services.Adapters.ModelCostServiceAdapter>());
            services.AddScoped<Interfaces.IVirtualKeyService, Services.Adapters.VirtualKeyServiceAdapter>();
            services.AddScoped<ConduitLLM.Core.Interfaces.IVirtualKeyService, ConduitLLM.WebUI.Services.RepositoryVirtualKeyService>();
            services.AddScoped<Interfaces.IIpFilterService, Services.Adapters.IpFilterServiceAdapter>();
            services.AddScoped<Interfaces.IProviderHealthService, Services.Adapters.ProviderHealthServiceAdapter>();
            services.AddScoped<Interfaces.ICostDashboardService, Services.Adapters.CostDashboardServiceAdapter>();
            services.AddScoped<Interfaces.IModelProviderMappingService, Services.Adapters.ModelProviderMappingServiceAdapter>();
            services.AddScoped<Interfaces.IRequestLogService, Services.Adapters.RequestLogServiceAdapter>();
            services.AddScoped<ConduitLLM.Configuration.Services.IRequestLogService>(sp => {
                var service = sp.GetRequiredService<Interfaces.IRequestLogService>();
                if (service is ConduitLLM.Configuration.Services.IRequestLogService configService) {
                    return configService;
                }
                throw new InvalidOperationException(
                    $"The registered IRequestLogService implementation ({service.GetType().FullName}) does not implement ConduitLLM.Configuration.Services.IRequestLogService");
            });
            
            // Register adapter services that use the Admin API
            services.AddScoped<Interfaces.IRouterService, Services.RouterServiceAdapter>();
            services.AddScoped<Interfaces.IDatabaseBackupService, Services.DatabaseBackupServiceAdapter>();

            return services;
        }
    }
}