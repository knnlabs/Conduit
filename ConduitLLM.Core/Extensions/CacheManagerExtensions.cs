using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

        /// <summary>
        /// Adds the cache registry for automatic discovery and management.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="autoDiscover">Whether to automatically discover cache regions on startup.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddCacheRegistry(this IServiceCollection services, bool autoDiscover = true)
        {
            // Register the cache registry as singleton
            services.AddSingleton<ICacheRegistry, CacheRegistry>();

            if (autoDiscover)
            {
                // Add hosted service for discovery
                services.AddHostedService<CacheDiscoveryHostedService>();
            }

            return services;
        }

        /// <summary>
        /// Adds the complete cache infrastructure with manager and registry.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The configuration.</param>
        /// <param name="autoDiscover">Whether to automatically discover cache regions.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddCacheInfrastructure(
            this IServiceCollection services, 
            IConfiguration configuration,
            bool autoDiscover = true)
        {
            // Ensure memory cache is registered
            services.AddMemoryCache();

            // Configure options from configuration
            services.Configure<CacheManagerOptions>(configuration.GetSection("CacheManager"));

            // Add cache registry
            services.AddCacheRegistry(autoDiscover);

            // Register cache manager with registry integration
            services.AddSingleton<ICacheManager>(provider =>
            {
                var memoryCache = provider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
                var distributedCache = provider.GetService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
                var logger = provider.GetRequiredService<ILogger<CacheManager>>();
                var options = provider.GetService<Microsoft.Extensions.Options.IOptions<CacheManagerOptions>>();
                var registry = provider.GetService<ICacheRegistry>();

                var cacheManager = new CacheManager(memoryCache, distributedCache, logger, options);

                // Sync configurations from registry if available
                if (registry != null)
                {
                    foreach (var (region, config) in registry.GetAllRegions())
                    {
                        cacheManager.UpdateRegionConfigAsync(config).GetAwaiter().GetResult();
                    }

                    // Subscribe to registry changes
                    registry.RegionUpdated += (sender, args) =>
                    {
                        cacheManager.UpdateRegionConfigAsync(args.Config).GetAwaiter().GetResult();
                    };
                }

                return cacheManager;
            });

            // Register health checks
            services.AddHealthChecks()
                .AddTypeActivatedCheck<CacheManagerHealthCheck>("cache_manager");

            return services;
        }

        /// <summary>
        /// Discovers and registers cache regions from specific assemblies.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="assemblies">Assemblies to scan.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection DiscoverCacheRegions(
            this IServiceCollection services,
            params Assembly[] assemblies)
        {
            services.AddSingleton<IHostedService>(provider =>
            {
                var registry = provider.GetRequiredService<ICacheRegistry>();
                var logger = provider.GetRequiredService<ILogger<CacheDiscoveryHostedService>>();
                return new CacheDiscoveryHostedService(registry, logger, assemblies);
            });

            return services;
        }
    }

    /// <summary>
    /// Hosted service for automatic cache region discovery.
    /// </summary>
    internal class CacheDiscoveryHostedService : IHostedService
    {
        private readonly ICacheRegistry _registry;
        private readonly ILogger<CacheDiscoveryHostedService> _logger;
        private readonly Assembly[]? _assemblies;

        public CacheDiscoveryHostedService(
            ICacheRegistry registry,
            ILogger<CacheDiscoveryHostedService> logger,
            Assembly[]? assemblies = null)
        {
            _registry = registry;
            _logger = logger;
            _assemblies = assemblies;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting cache region discovery...");

            try
            {
                var count = await _registry.DiscoverRegionsAsync(_assemblies);
                _logger.LogInformation("Cache region discovery completed. Found {Count} regions", count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to discover cache regions");
                // Don't throw - cache discovery failure shouldn't prevent app startup
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}