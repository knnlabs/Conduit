using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using ConduitLLM.Core.HealthChecks;

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
            services.Configure<CacheStatisticsOptions>(configuration.GetSection("CacheStatistics"));

            // Register statistics collector (local mode only)
            services.AddSingleton<ICacheStatisticsCollector>(sp =>
            {
                return new CacheStatisticsCollector(
                    sp.GetRequiredService<ILogger<CacheStatisticsCollector>>(),
                    sp.GetRequiredService<IOptions<CacheStatisticsOptions>>(),
                    sp.GetService<ICacheStatisticsStore>());
            });

            // Register policy engine
            services.AddSingleton<ICachePolicyEngine, CachePolicyEngine>();

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

            // Register statistics collector with default options
            services.AddSingleton<ICacheStatisticsCollector, CacheStatisticsCollector>();

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

            // Register statistics store for Redis
            services.AddSingleton<ICacheStatisticsStore, RedisCacheStatisticsStore>();

            // Register Redis connection multiplexer with lazy initialization
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<CacheManager>>();
                logger.LogInformation("Creating Redis connection multiplexer (lazy initialization)");
                
                // Parse connection string and set non-blocking options
                var configOptions = ConfigurationOptions.Parse(redisConnectionString);
                configOptions.AbortOnConnectFail = false; // Don't block on startup
                configOptions.ConnectTimeout = 5000; // 5 second timeout
                configOptions.ConnectRetry = 3;
                
                try
                {
                    var multiplexer = ConnectionMultiplexer.Connect(configOptions);
                    logger.LogInformation("Redis connection multiplexer created successfully");
                    return multiplexer;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to create Redis connection multiplexer. Cache functionality may be degraded.");
                    throw;
                }
            });

            // Register distributed statistics collector
            services.AddSingleton<IDistributedCacheStatisticsCollector, RedisCacheStatisticsCollector>();
            
            // Register statistics health check
            services.AddSingleton<IStatisticsHealthCheck, CacheStatisticsHealthCheck>();
            services.AddHostedService(sp => 
            {
                var healthCheck = sp.GetRequiredService<IStatisticsHealthCheck>() as CacheStatisticsHealthCheck;
                return healthCheck ?? throw new InvalidOperationException("IStatisticsHealthCheck must be implemented by CacheStatisticsHealthCheck");
            });

            // Register hybrid collector as the main statistics collector
            services.AddSingleton<ICacheStatisticsCollector>(sp =>
            {
                var distributedCollector = sp.GetService<IDistributedCacheStatisticsCollector>();
                var localCollector = new CacheStatisticsCollector(
                    sp.GetRequiredService<ILogger<CacheStatisticsCollector>>(),
                    sp.GetRequiredService<IOptions<CacheStatisticsOptions>>(),
                    sp.GetService<ICacheStatisticsStore>());
                
                return new HybridCacheStatisticsCollector(
                    localCollector,
                    distributedCollector,
                    sp.GetRequiredService<ILogger<HybridCacheStatisticsCollector>>());
            });

            // Register policy engine
            services.AddSingleton<ICachePolicyEngine, CachePolicyEngine>();

            // Register the cache manager as singleton
            services.AddSingleton<ICacheManager, CacheManager>();

            // Register health checks
            services.AddHealthChecks()
                .AddTypeActivatedCheck<CacheManagerHealthCheck>("cache_manager")
                .AddRedis(redisConnectionString, name: "redis_cache")
                .AddCheck<CacheStatisticsHealthCheckAdapter>("cache_statistics");

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
                // Now runs as a BackgroundService with delayed startup to avoid blocking
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
            bool autoDiscover = false) // DISABLED: Issue #562 - causes startup hang
        {
            // Ensure memory cache is registered
            services.AddMemoryCache();

            // Configure options from configuration
            services.Configure<CacheManagerOptions>(configuration.GetSection("CacheManager"));
            services.Configure<CacheStatisticsOptions>(configuration.GetSection("CacheStatistics"));

            // Add cache registry
            services.AddCacheRegistry(autoDiscover);

            // Register policy engine
            services.AddSingleton<ICachePolicyEngine, CachePolicyEngine>();

            // Check if we have Redis configuration for statistics store
            var redisConnection = configuration.GetConnectionString("Redis") ?? configuration["Redis:Configuration"];
            if (!string.IsNullOrEmpty(redisConnection))
            {
                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = redisConnection;
                    options.InstanceName = "conduit:cache:";
                });
                services.AddSingleton<ICacheStatisticsStore, RedisCacheStatisticsStore>();
                
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
                    
                    try
                    {
                        return ConnectionMultiplexer.Connect(configOptions);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to create Redis connection. Cache functionality may be degraded.");
                        throw;
                    }
                });

                // Register distributed statistics collector
                services.AddSingleton<IDistributedCacheStatisticsCollector, RedisCacheStatisticsCollector>();
                
                // Register statistics health check
                // IMPORTANT: Disabled to prevent startup hang (issue #562)
                // Even with non-blocking Redis connection, this service causes hangs
                // TODO: Implement proper async initialization pattern
                // services.AddSingleton<IStatisticsHealthCheck, CacheStatisticsHealthCheck>();
                // services.AddHostedService(sp => 
                // {
                //     var healthCheck = sp.GetRequiredService<IStatisticsHealthCheck>() as CacheStatisticsHealthCheck;
                //     return healthCheck ?? throw new InvalidOperationException("IStatisticsHealthCheck must be implemented by CacheStatisticsHealthCheck");
                // });
            }

            // Register statistics collector (hybrid if Redis is available, local otherwise)
            services.AddSingleton<ICacheStatisticsCollector>(sp =>
            {
                var distributedCollector = sp.GetService<IDistributedCacheStatisticsCollector>();
                var localCollector = new CacheStatisticsCollector(
                    sp.GetRequiredService<ILogger<CacheStatisticsCollector>>(),
                    sp.GetRequiredService<IOptions<CacheStatisticsOptions>>(),
                    sp.GetService<ICacheStatisticsStore>());
                
                if (distributedCollector != null)
                {
                    return new HybridCacheStatisticsCollector(
                        localCollector,
                        distributedCollector,
                        sp.GetRequiredService<ILogger<HybridCacheStatisticsCollector>>());
                }
                
                return localCollector;
            });

            // Register cache manager with registry integration
            services.AddSingleton<ICacheManager>(provider =>
            {
                var memoryCache = provider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
                var distributedCache = provider.GetService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
                var logger = provider.GetRequiredService<ILogger<CacheManager>>();
                var options = provider.GetService<Microsoft.Extensions.Options.IOptions<CacheManagerOptions>>();
                var registry = provider.GetService<ICacheRegistry>();
                var statisticsCollector = provider.GetService<ICacheStatisticsCollector>();

                var cacheManager = new CacheManager(memoryCache, distributedCache, logger, options, statisticsCollector);

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
    internal class CacheDiscoveryHostedService : BackgroundService
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

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Delay discovery to avoid blocking startup
            // This allows the application to start while discovery happens in the background
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            
            if (stoppingToken.IsCancellationRequested)
                return;

            _logger.LogInformation("Starting background cache region discovery...");

            try
            {
                // Run the potentially slow assembly scanning in a background task
                await Task.Run(async () =>
                {
                    var count = await _registry.DiscoverRegionsAsync(_assemblies);
                    _logger.LogInformation("Cache region discovery completed. Found {Count} regions", count);
                }, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Cache region discovery was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to discover cache regions");
                // Don't throw - cache discovery failure shouldn't prevent app startup
            }
        }
    }
}