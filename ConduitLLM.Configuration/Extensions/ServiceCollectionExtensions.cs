using System;
using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Configuration.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
            // Register DbContext interface
            services.AddScoped<IConfigurationDbContext>(provider => 
                provider.GetRequiredService<ConfigurationDbContext>());
                
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
            services.AddScoped<IProviderHealthRepository, ProviderHealthRepository>();
            services.AddScoped<IIpFilterRepository, IpFilterRepository>();

            return services;
        }
        
        /// <summary>
        /// Adds caching services to the service collection
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">The application configuration</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddCachingServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Register cache options
            services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));
            
            // Add memory cache
            services.AddMemoryCache(options =>
            {
                var cacheSection = configuration.GetSection(CacheOptions.SectionName);
                options.SizeLimit = cacheSection.GetValue<long?>("MaxCacheItems");
            });
            
            // Add Redis connection factory
            services.AddSingleton<RedisConnectionFactory>();
            
            // Register cache service factory
            services.AddSingleton<CacheServiceFactory>();
            
            // Register the appropriate distributed cache provider based on configuration
            var cacheType = configuration.GetSection(CacheOptions.SectionName)
                .GetValue<string>("CacheType")?.ToLowerInvariant();
                
            if (cacheType == "redis")
            {
                var redisConnectionString = configuration.GetSection(CacheOptions.SectionName)
                    .GetValue<string>("RedisConnectionString");
                    
                var redisInstanceName = configuration.GetSection(CacheOptions.SectionName)
                    .GetValue<string>("RedisInstanceName") ?? "conduitllm-cache";
                
                if (!string.IsNullOrEmpty(redisConnectionString))
                {
                    services.AddStackExchangeRedisCache(options =>
                    {
                        options.Configuration = redisConnectionString;
                        options.InstanceName = redisInstanceName;
                    });
                }
                else
                {
                    // Fall back to memory cache if Redis connection string is not configured
                    services.AddDistributedMemoryCache();
                }
            }
            else
            {
                // Use memory cache if Redis is not specified
                services.AddDistributedMemoryCache();
            }
            
            // Register the ICacheService as a singleton but with factory-based initialization
            services.AddSingleton<ICacheService>(serviceProvider =>
            {
                var factory = serviceProvider.GetRequiredService<CacheServiceFactory>();
                
                // Create the appropriate cache service based on configuration
                // For simplicity in the synchronous service provider context, we'll block on the async result here
                return factory.CreateCacheServiceAsync().GetAwaiter().GetResult();
            });
            
            return services;
        }
    }
}