using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.WebUI.Services;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ConduitLLM.WebUI.Extensions
{
    /// <summary>
    /// Extension methods for adding repository pattern services to the service collection
    /// </summary>
    public static class RepositoryServiceExtensions
    {
        /// <summary>
        /// Adds repository-based services to the IServiceCollection
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <returns>The modified service collection</returns>
        public static IServiceCollection AddRepositoryServices(this IServiceCollection services)
        {
            // Register repositories from ConduitLLM.Configuration
            services.AddRepositories();
            
            // Register the repository-based service implementations
            services.AddScoped<IVirtualKeyService, VirtualKeyService>();
            services.AddScoped<IRequestLogService, RequestLogService>();
            services.AddScoped<ICostDashboardService, CostDashboardService>();
            services.AddScoped<IRouterService, RouterService>();
            services.AddScoped<IGlobalSettingService, GlobalSettingService>();
            services.AddScoped<IIpFilterService, IpFilterService>();

            // Register IP filter options
            services.Configure<ConduitLLM.Configuration.Options.IpFilterOptions>(options => {
                options.Enabled = Environment.GetEnvironmentVariable(
                    ConduitLLM.Configuration.Constants.IpFilterConstants.IP_FILTERING_ENABLED_ENV)?.ToLower() == "true";
                options.DefaultAllow = true;
                options.BypassForAdminUi = true;
                options.EnableIpv6 = true;
                options.ExcludedEndpoints = new List<string> { "/api/v1/health" };
            });

            return services;
        }
    }
}