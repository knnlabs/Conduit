using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Extensions
{
    /// <summary>
    /// Extension methods for registering cache manager services.
    /// </summary>
    public static class CacheManagerExtensions
    {
        /// <summary>
        /// Adds the unified cache manager to the service collection.
        /// Automatically detects Redis configuration and uses distributed statistics if available.
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

            // Check if Redis is configured for the distributed cache tier
            var redisConnection = configuration.GetConnectionString("Redis") ?? configuration["Redis:Configuration"];
            if (!string.IsNullOrEmpty(redisConnection))
            {
                // Add Redis distributed cache
                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = redisConnection;
                    options.InstanceName = "conduit:cache:";
                });

                // Register Redis connection multiplexer with lazy initialization
                services.TryAddSingleton<IConnectionMultiplexer>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<CacheManager>>();
                    logger.LogInformation("Creating Redis connection for the distributed cache tier");

                    var configOptions = ConfigurationOptions.Parse(redisConnection);
                    configOptions.AbortOnConnectFail = false;
                    configOptions.ConnectTimeout = 5000;
                    configOptions.ConnectRetry = 3;

                    return ConnectionMultiplexer.Connect(configOptions);
                });
            }

            // Register the cache manager as singleton
            services.AddSingleton<ICacheManager, CacheManager>();

            // Health checks removed per YAGNI principle

            return services;
        }

        /// <summary>
        /// Adds the cache registry for cache region management.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="autoDiscover">DEPRECATED: Auto-discovery is no longer supported. This parameter is ignored.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddCacheRegistry(this IServiceCollection services, bool autoDiscover = false)
        {
            // Register the cache registry as singleton
            services.AddSingleton<ICacheRegistry, CacheRegistry>();

            // Auto-discovery is permanently disabled to prevent startup hangs (Issue #562)
            // All standard cache regions are pre-registered in CacheRegistry.InitializeDefaultRegions()
            // For custom regions, use configuration-based registration instead
            if (autoDiscover)
            {
                throw new NotSupportedException(
                    "Cache auto-discovery is no longer supported due to performance issues. " +
                    "All standard cache regions are automatically registered. " +
                    "For custom regions, use configuration-based registration.");
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
            bool autoDiscover = false) // DISABLED: Issue #562 - causes startup hang
        {
            // Ensure memory cache is registered
            services.AddMemoryCache();

            // Configure options from configuration
            services.Configure<CacheManagerOptions>(configuration.GetSection("CacheManager"));

            // Add cache registry
            services.AddCacheRegistry(autoDiscover);

            // Check if we have Redis configuration for the distributed cache tier
            var redisConnection = configuration.GetConnectionString("Redis") ?? configuration["Redis:Configuration"];
            if (!string.IsNullOrEmpty(redisConnection))
            {
                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = redisConnection;
                    options.InstanceName = "conduit:cache:";
                });

                // Use existing RedisConnectionFactory if available, otherwise register a lazy connection
                services.TryAddSingleton<IConnectionMultiplexer>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<CacheManager>>();

                    // Parse connection string and set non-blocking options
                    logger.LogInformation("Creating Redis connection for cache infrastructure: {Connection}",
                        redisConnection.Contains("password=") ? redisConnection.Replace("password=", "password=******") : redisConnection);
                    var configOptions = ConfigurationOptions.Parse(redisConnection);
                    configOptions.AbortOnConnectFail = false; // Don't block on startup
                    configOptions.ConnectTimeout = 5000; // 5 second timeout
                    configOptions.ConnectRetry = 3;

                    return ConnectionMultiplexer.Connect(configOptions);
                });
            }

            // Register cache manager with registry integration
            services.AddSingleton<ICacheManager>(provider =>
            {
                var memoryCache = provider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
                var distributedCache = provider.GetService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
                var logger = provider.GetRequiredService<ILogger<CacheManager>>();
                var options = provider.GetService<Microsoft.Extensions.Options.IOptions<CacheManagerOptions>>();
                var registry = provider.GetService<ICacheRegistry>();

                var cacheManager = new CacheManager(memoryCache, distributedCache, logger, options);

                // Defer registry sync to avoid blocking during startup
                if (registry != null)
                {
                    // Use Task.Run to sync configurations in the background
                    Task.Run(async () =>
                    {
                        try
                        {
                            foreach (var (region, config) in registry.GetAllRegions())
                            {
                                await cacheManager.UpdateRegionConfigAsync(config);
                            }
                            logger.LogInformation("Cache manager synchronized with registry");
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Failed to sync cache configurations from registry");
                        }
                    });

                    // Subscribe to registry changes (make async)
                    registry.RegionUpdated += async (sender, args) =>
                    {
                        try
                        {
                            await cacheManager.UpdateRegionConfigAsync(args.Config);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Failed to update cache region {Region}", args.Region);
                        }
                    };
                }

                return cacheManager;
            });

            // Health checks removed per YAGNI principle

            return services;
        }

    }
}
