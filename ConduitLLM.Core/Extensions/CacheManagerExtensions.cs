using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;

namespace ConduitLLM.Core.Extensions
{
    /// <summary>
    /// Extension methods for registering cache manager services.
    /// </summary>
    public static class CacheManagerExtensions
    {
        /// <summary>
        /// Adds the unified cache manager to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The configuration.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddCacheManager(this IServiceCollection services, IConfiguration configuration)
        {
            // Ensure memory cache is registered
            services.AddMemoryCache();

            // Configure options from configuration
            services.Configure<CacheManagerOptions>(configuration.GetSection("CacheManager"));

            // Register the cache manager as singleton
            services.AddSingleton<ICacheManager, CacheManager>();

            // Register health checks
            services.AddHealthChecks()
                .AddTypeActivatedCheck<CacheManagerHealthCheck>("cache_manager");

            return services;
        }

        /// <summary>
        /// Adds the unified cache manager with custom options.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configureOptions">Action to configure options.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddCacheManager(this IServiceCollection services, Action<CacheManagerOptions> configureOptions)
        {
            // Ensure memory cache is registered
            services.AddMemoryCache();

            // Configure options
            services.Configure(configureOptions);

            // Register the cache manager as singleton
            services.AddSingleton<ICacheManager, CacheManager>();

            // Register health checks
            services.AddHealthChecks()
                .AddTypeActivatedCheck<CacheManagerHealthCheck>("cache_manager");

            return services;
        }

        /// <summary>
        /// Adds the unified cache manager with Redis distributed cache.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="redisConnectionString">Redis connection string.</param>
        /// <param name="configureOptions">Optional action to configure options.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddCacheManagerWithRedis(
            this IServiceCollection services, 
            string redisConnectionString,
            Action<CacheManagerOptions>? configureOptions = null)
        {
            // Ensure memory cache is registered
            services.AddMemoryCache();

            // Add Redis distributed cache
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "conduit:cache:";
            });

            // Configure options
            if (configureOptions != null)
            {
                services.Configure(configureOptions);
            }

            // Register the cache manager as singleton
            services.AddSingleton<ICacheManager, CacheManager>();

            // Register health checks
            services.AddHealthChecks()
                .AddTypeActivatedCheck<CacheManagerHealthCheck>("cache_manager")
                .AddRedis(redisConnectionString, name: "redis_cache");

            return services;
        }
    }
}