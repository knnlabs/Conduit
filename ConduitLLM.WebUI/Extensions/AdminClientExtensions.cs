using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Options;
using ConduitLLM.WebUI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

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
        /// This allows WebUI to use either direct repository access or the AdminApiClient based on configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The configuration.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddAdminApiAdapters(this IServiceCollection services, IConfiguration configuration)
        {
            // Determine whether to use Admin API or direct repository access
            bool useAdminApi = false;
            var useAdminApiStr = configuration["CONDUIT_USE_ADMIN_API"];
            if (!string.IsNullOrEmpty(useAdminApiStr))
            {
                bool.TryParse(useAdminApiStr, out useAdminApi);
            }

            if (useAdminApi)
            {
                // Register adapters that use the Admin API client
                services.AddScoped<Interfaces.IGlobalSettingService, Services.Adapters.GlobalSettingServiceAdapter>();
                services.AddScoped<Interfaces.IProviderHealthService, Services.Adapters.ProviderHealthServiceAdapter>();
                services.AddScoped<Interfaces.IModelCostService, Services.Adapters.ModelCostServiceAdapter>();
                services.AddScoped<Interfaces.IProviderCredentialService, Services.Adapters.ProviderCredentialServiceAdapter>();
                services.AddScoped<Interfaces.IVirtualKeyService, Services.Adapters.VirtualKeyServiceAdapter>();
                services.AddScoped<Interfaces.IRequestLogService, Services.Adapters.RequestLogServiceAdapter>();
                services.AddScoped<Interfaces.IIpFilterService, Services.Adapters.IpFilterServiceAdapter>();
                services.AddScoped<Interfaces.ICostDashboardService, Services.Adapters.CostDashboardServiceAdapter>();
                services.AddScoped<Interfaces.IModelProviderMappingService, Services.Adapters.ModelProviderMappingServiceAdapter>();
                services.AddScoped<Interfaces.IRouterService, Services.RouterServiceAdapter>();
                services.AddScoped<Interfaces.IDatabaseBackupService, Services.DatabaseBackupServiceAdapter>();
            }
            else
            {
                // Register services that use direct repository access
                services.AddScoped<Interfaces.IGlobalSettingService, Services.GlobalSettingService>();
                services.AddScoped<Interfaces.IProviderHealthService, Services.ProviderHealthService>();
                services.AddScoped<Interfaces.IModelCostService, Services.ModelCostService>();
                services.AddScoped<Interfaces.IProviderCredentialService, Services.ProviderCredentialService>();
                services.AddScoped<Interfaces.IVirtualKeyService, Services.VirtualKeyService>();
                services.AddScoped<Interfaces.IRequestLogService, Services.RequestLogService>();
                services.AddScoped<Interfaces.IIpFilterService, Services.IpFilterService>();
                services.AddScoped<Interfaces.ICostDashboardService, Services.CostDashboardService>();
                services.AddScoped<Interfaces.IModelProviderMappingService, Services.ModelProviderMappingService>();
                services.AddScoped<Interfaces.IRouterService, Services.RouterService>();
                services.AddScoped<Interfaces.IDatabaseBackupService, Services.DatabaseBackupService>();
            }

            return services;
        }
    }
}