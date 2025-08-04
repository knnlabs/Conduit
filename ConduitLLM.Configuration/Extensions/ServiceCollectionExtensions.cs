using System;

using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Configuration.Services;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
            services.AddScoped<IVirtualKeyGroupRepository, VirtualKeyGroupRepository>();
            services.AddScoped<IProviderRepository, ProviderRepository>();
            services.AddScoped<IProviderKeyCredentialRepository, ProviderKeyCredentialRepository>();
            services.AddScoped<IGlobalSettingRepository, GlobalSettingRepository>();
            services.AddScoped<IModelProviderMappingRepository, ModelProviderMappingRepository>();
            services.AddScoped<IModelCostRepository, ModelCostRepository>();
            services.AddScoped<IRequestLogRepository, RequestLogRepository>();
            
            // Register validator
            services.AddScoped<ProviderKeyCredentialValidator>();

            // Register new repositories
            services.AddScoped<INotificationRepository, NotificationRepository>();
            services.AddScoped<IVirtualKeySpendHistoryRepository, VirtualKeySpendHistoryRepository>();
            services.AddScoped<IRouterConfigRepository, RouterConfigRepository>();
            services.AddScoped<IModelDeploymentRepository, ModelDeploymentRepository>();
            services.AddScoped<IFallbackConfigurationRepository, FallbackConfigurationRepository>();
            services.AddScoped<IFallbackModelMappingRepository, FallbackModelMappingRepository>();
            services.AddScoped<IIpFilterRepository, IpFilterRepository>();

            // Register audio-related repositories
            services.AddScoped<IAudioProviderConfigRepository, AudioProviderConfigRepository>();
            services.AddScoped<IAudioCostRepository, AudioCostRepository>();
            services.AddScoped<IAudioUsageLogRepository, AudioUsageLogRepository>();

            // Register async task repository
            services.AddScoped<IAsyncTaskRepository, AsyncTaskRepository>();

            // Register media record repository
            services.AddScoped<IMediaRecordRepository, MediaRecordRepository>();

            // Register cache configuration service
            services.AddScoped<ICacheConfigurationService, CacheConfigurationService>();

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
                var logger = serviceProvider.GetRequiredService<ILogger<CacheServiceFactory>>();
                logger.LogInformation("[CacheService] Creating cache service during service registration...");
                
                var factory = serviceProvider.GetRequiredService<CacheServiceFactory>();

                // Create the appropriate cache service based on configuration
                // For simplicity in the synchronous service provider context, we'll block on the async result here
                var cacheService = factory.CreateCacheServiceAsync().GetAwaiter().GetResult();
                logger.LogInformation("[CacheService] Cache service created successfully");
                return cacheService;
            });

            return services;
        }

        /// <summary>
        /// Adds database initialization services to the service collection
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddDatabaseInitialization(this IServiceCollection services)
        {
            // Register the simple migration service
            services.AddScoped<SimpleMigrationService>();

            return services;
        }

    }
}
