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
                var baseUrl = configuration["CONDUIT_ADMIN_API_BASE_URL"] ?? configuration["CONDUIT_ADMIN_API_URL"];
                if (!string.IsNullOrEmpty(baseUrl))
                {
                    options.BaseUrl = baseUrl;
                    Console.WriteLine($"[AdminClient] Admin API URL set to: {baseUrl}");
                }

                var masterKey = configuration["CONDUIT_MASTER_KEY"] ?? configuration["AdminApi__MasterKey"];
                if (!string.IsNullOrEmpty(masterKey))
                {
                    options.MasterKey = masterKey;
                    Console.WriteLine($"[AdminClient] Using master key with length: {masterKey.Length}");
                }
                else
                {
                    Console.WriteLine("[AdminClient] WARNING: No master key found in configuration!");
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
        /// Adds direct service registrations for all interfaces implemented by the AdminApiClient.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The configuration.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddDirectApiServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Register all interface implementations using the already registered AdminApiClient instance
            services.AddScoped<Interfaces.IGlobalSettingService>(sp => {
                var client = sp.GetRequiredService<Interfaces.IAdminApiClient>();
                return client as Interfaces.IGlobalSettingService ?? 
                    throw new InvalidOperationException($"AdminApiClient does not implement IGlobalSettingService");
            });
            
            services.AddScoped<Interfaces.IProviderCredentialService>(sp => {
                var client = sp.GetRequiredService<Interfaces.IAdminApiClient>();
                return client as Interfaces.IProviderCredentialService ?? 
                    throw new InvalidOperationException($"AdminApiClient does not implement IProviderCredentialService");
            });
            
            services.AddScoped<Interfaces.IModelCostService>(sp => {
                var client = sp.GetRequiredService<Interfaces.IAdminApiClient>();
                return client as Interfaces.IModelCostService ?? 
                    throw new InvalidOperationException($"AdminApiClient does not implement IModelCostService");
            });
            
            services.AddScoped<Interfaces.IVirtualKeyService>(sp => {
                var client = sp.GetRequiredService<Interfaces.IAdminApiClient>();
                return client as Interfaces.IVirtualKeyService ?? 
                    throw new InvalidOperationException($"AdminApiClient does not implement IVirtualKeyService");
            });
            
            services.AddScoped<Interfaces.IIpFilterService>(sp => {
                var client = sp.GetRequiredService<Interfaces.IAdminApiClient>();
                return client as Interfaces.IIpFilterService ?? 
                    throw new InvalidOperationException($"AdminApiClient does not implement IIpFilterService");
            });
            
            services.AddScoped<Interfaces.IProviderHealthService>(sp => {
                var client = sp.GetRequiredService<Interfaces.IAdminApiClient>();
                return client as Interfaces.IProviderHealthService ?? 
                    throw new InvalidOperationException($"AdminApiClient does not implement IProviderHealthService");
            });
            
            services.AddScoped<Interfaces.ICostDashboardService>(sp => {
                var client = sp.GetRequiredService<Interfaces.IAdminApiClient>();
                return client as Interfaces.ICostDashboardService ?? 
                    throw new InvalidOperationException($"AdminApiClient does not implement ICostDashboardService");
            });
            
            services.AddScoped<Interfaces.IModelProviderMappingService>(sp => {
                var client = sp.GetRequiredService<Interfaces.IAdminApiClient>();
                return client as Interfaces.IModelProviderMappingService ?? 
                    throw new InvalidOperationException($"AdminApiClient does not implement IModelProviderMappingService");
            });
            
            services.AddScoped<Interfaces.IRequestLogService>(sp => {
                var client = sp.GetRequiredService<Interfaces.IAdminApiClient>();
                return client as Interfaces.IRequestLogService ?? 
                    throw new InvalidOperationException($"AdminApiClient does not implement IRequestLogService");
            });
            
            services.AddScoped<Interfaces.IRouterService>(sp => {
                var client = sp.GetRequiredService<Interfaces.IAdminApiClient>();
                return client as Interfaces.IRouterService ?? 
                    throw new InvalidOperationException($"AdminApiClient does not implement IRouterService");
            });
            
            services.AddScoped<Interfaces.IDatabaseBackupService>(sp => {
                var client = sp.GetRequiredService<Interfaces.IAdminApiClient>();
                return client as Interfaces.IDatabaseBackupService ?? 
                    throw new InvalidOperationException($"AdminApiClient does not implement IDatabaseBackupService");
            });
            
            services.AddScoped<Interfaces.IHttpRetryConfigurationService>(sp => {
                var client = sp.GetRequiredService<Interfaces.IAdminApiClient>();
                return client as Interfaces.IHttpRetryConfigurationService ?? 
                    throw new InvalidOperationException($"AdminApiClient does not implement IHttpRetryConfigurationService");
            });
            
            services.AddScoped<Interfaces.IHttpTimeoutConfigurationService>(sp => {
                var client = sp.GetRequiredService<Interfaces.IAdminApiClient>();
                return client as Interfaces.IHttpTimeoutConfigurationService ?? 
                    throw new InvalidOperationException($"AdminApiClient does not implement IHttpTimeoutConfigurationService");
            });
            
            services.AddScoped<Interfaces.IProviderStatusService>(sp => {
                var client = sp.GetRequiredService<Interfaces.IAdminApiClient>();
                return client as Interfaces.IProviderStatusService ?? 
                    throw new InvalidOperationException($"AdminApiClient does not implement IProviderStatusService");
            });

            // Register required Configuration.Services interfaces that depend on WebUI interfaces
            services.AddScoped<ConduitLLM.Configuration.Services.IModelCostService>(sp => {
                var service = sp.GetRequiredService<Interfaces.IModelCostService>();
                return service as ConduitLLM.Configuration.Services.IModelCostService ?? 
                    throw new InvalidOperationException(
                        $"The registered IModelCostService implementation ({service.GetType().FullName}) does not implement ConduitLLM.Configuration.Services.IModelCostService");
            });
            services.AddScoped<ConduitLLM.Configuration.Services.IRequestLogService>(sp => {
                var service = sp.GetRequiredService<Interfaces.IRequestLogService>();
                if (service is ConduitLLM.Configuration.Services.IRequestLogService configService) {
                    return configService;
                }
                throw new InvalidOperationException(
                    $"The registered IRequestLogService implementation ({service.GetType().FullName}) does not implement ConduitLLM.Configuration.Services.IRequestLogService");
            });

            // Register VirtualKeyService for Core.Interfaces.IVirtualKeyService
            services.AddScoped<ConduitLLM.Core.Interfaces.IVirtualKeyService, ConduitLLM.WebUI.Services.RepositoryVirtualKeyService>();

            return services;
        }
    }
}