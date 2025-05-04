using ConduitLLM.Configuration.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace ConduitLLM.Configuration.Extensions
{
    /// <summary>
    /// Extension methods for configuring repository services
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds repository services to the service collection
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddRepositories(this IServiceCollection services)
        {
            // Register repositories
            services.AddScoped<IVirtualKeyRepository, VirtualKeyRepository>();
            services.AddScoped<IProviderCredentialRepository, ProviderCredentialRepository>();
            services.AddScoped<IGlobalSettingRepository, GlobalSettingRepository>();
            services.AddScoped<IModelProviderMappingRepository, ModelProviderMappingRepository>();
            services.AddScoped<IModelCostRepository, ModelCostRepository>();
            services.AddScoped<IRequestLogRepository, RequestLogRepository>();
            
            // Register new repositories
            services.AddScoped<INotificationRepository, NotificationRepository>();
            services.AddScoped<IVirtualKeySpendHistoryRepository, VirtualKeySpendHistoryRepository>();
            services.AddScoped<IRouterConfigRepository, RouterConfigRepository>();
            services.AddScoped<IModelDeploymentRepository, ModelDeploymentRepository>();
            services.AddScoped<IFallbackConfigurationRepository, FallbackConfigurationRepository>();
            services.AddScoped<IFallbackModelMappingRepository, FallbackModelMappingRepository>();
            
            return services;
        }
    }
}