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
            
            return services;
        }
    }
}