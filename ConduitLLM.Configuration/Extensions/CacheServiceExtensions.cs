using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Configuration.Services;
using ConduitLLM.Configuration.Utilities;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Extensions
{
    /// <summary>
    /// Extension methods for configuring cache services
    /// </summary>
    public static class CacheServiceExtensions
    {
        /// <summary>
        /// Adds the cache service to the service collection with options from configuration
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">The configuration</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddCacheService(this IServiceCollection services, IConfiguration configuration)
        {
            // Register memory cache if not already registered
            services.AddMemoryCache();

            // Configure options from configuration and environment variables
            services.Configure<CacheOptions>(options =>
            {
                // Get values from configuration section
                var section = configuration.GetSection(CacheOptions.SectionName);

                // Manually set properties from configuration
                if (int.TryParse(section["DefaultAbsoluteExpirationMinutes"], out var absMinutes))
                {
                    options.DefaultAbsoluteExpirationMinutes = absMinutes;
                }

                if (int.TryParse(section["DefaultSlidingExpirationMinutes"], out var slidingMinutes))
                {
                    options.DefaultSlidingExpirationMinutes = slidingMinutes;
                }

                if (bool.TryParse(section["UseDefaultExpirationTimes"], out var useDefault))
                {
                    options.UseDefaultExpirationTimes = useDefault;
                }

                if (bool.TryParse(section["IsEnabled"], out var isEnabled))
                {
                    options.IsEnabled = isEnabled;
                }

                if (section["CacheType"] != null)
                {
                    options.CacheType = section["CacheType"] ?? options.CacheType;
                }

                if (int.TryParse(section["DefaultExpirationMinutes"], out var defaultExpMinutes))
                {
                    options.DefaultExpirationMinutes = defaultExpMinutes;
                }

                if (int.TryParse(section["MaxCacheItems"], out var maxItems))
                {
                    options.MaxCacheItems = maxItems;
                }

                if (section["RedisConnectionString"] != null)
                {
                    options.RedisConnectionString = section["RedisConnectionString"] ?? options.RedisConnectionString;
                }

                if (section["RedisInstanceName"] != null)
                {
                    options.RedisInstanceName = section["RedisInstanceName"] ?? options.RedisInstanceName;
                }

                if (bool.TryParse(section["IncludeModelInKey"], out var includeModel))
                {
                    options.IncludeModelInKey = includeModel;
                }

                if (bool.TryParse(section["IncludeProviderInKey"], out var includeProvider))
                {
                    options.IncludeProviderInKey = includeProvider;
                }

                if (bool.TryParse(section["IncludeApiKeyInKey"], out var includeApiKey))
                {
                    options.IncludeApiKeyInKey = includeApiKey;
                }

                if (bool.TryParse(section["IncludeTemperatureInKey"], out var includeTemp))
                {
                    options.IncludeTemperatureInKey = includeTemp;
                }

                if (bool.TryParse(section["IncludeMaxTokensInKey"], out var includeMaxTokens))
                {
                    options.IncludeMaxTokensInKey = includeMaxTokens;
                }

                if (bool.TryParse(section["IncludeTopPInKey"], out var includeTopP))
                {
                    options.IncludeTopPInKey = includeTopP;
                }

                if (section["HashAlgorithm"] != null)
                {
                    options.HashAlgorithm = section["HashAlgorithm"] ?? options.HashAlgorithm;
                }

                // Override with environment variables if present
                var absoluteExpEnv = Environment.GetEnvironmentVariable("CONDUIT_CACHE_ABSOLUTE_EXPIRATION_MINUTES");
                if (!string.IsNullOrEmpty(absoluteExpEnv) && int.TryParse(absoluteExpEnv, out var absMinutesEnv))
                {
                    options.DefaultAbsoluteExpirationMinutes = absMinutesEnv;
                }

                var slidingExpEnv = Environment.GetEnvironmentVariable("CONDUIT_CACHE_SLIDING_EXPIRATION_MINUTES");
                if (!string.IsNullOrEmpty(slidingExpEnv) && int.TryParse(slidingExpEnv, out var slideMinutesEnv))
                {
                    options.DefaultSlidingExpirationMinutes = slideMinutesEnv;
                }

                var useDefaultExpEnv = Environment.GetEnvironmentVariable("CONDUIT_CACHE_USE_DEFAULT_EXPIRATION");
                if (!string.IsNullOrEmpty(useDefaultExpEnv) && bool.TryParse(useDefaultExpEnv, out var useDefaultEnv))
                {
                    options.UseDefaultExpirationTimes = useDefaultEnv;
                }

                var enabledEnv = Environment.GetEnvironmentVariable("CONDUIT_CACHE_ENABLED");
                if (!string.IsNullOrEmpty(enabledEnv) && bool.TryParse(enabledEnv, out var enabledEnvValue))
                {
                    options.IsEnabled = enabledEnvValue;
                }

                var cacheTypeEnv = Environment.GetEnvironmentVariable("CONDUIT_CACHE_TYPE");
                if (!string.IsNullOrEmpty(cacheTypeEnv))
                {
                    options.CacheType = cacheTypeEnv;
                }
            });

            // Configure Redis connection string from environment variables
            // Priority: REDIS_URL -> CONDUIT_REDIS_CONNECTION_STRING -> config
            services.Configure<CacheOptions>(options =>
            {
                var redisUrl = Environment.GetEnvironmentVariable("REDIS_URL");
                var legacyConnectionString = Environment.GetEnvironmentVariable("CONDUIT_REDIS_CONNECTION_STRING");
                
                if (!string.IsNullOrEmpty(redisUrl))
                {
                    // Get logger for validation
                    var loggerFactory = services.BuildServiceProvider().GetService<ILoggerFactory>();
                    var logger = loggerFactory?.CreateLogger("CacheConfiguration") ?? 
                                Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
                    
                    // Validate the Redis URL
                    var isValid = RedisUrlValidator.ValidateAndLog(redisUrl, logger, "CacheService");
                    
                    if (isValid)
                    {
                        try
                        {
                            // Parse Redis URL format into connection string
                            options.RedisConnectionString = RedisUrlParser.ParseRedisUrl(redisUrl);
                            // IsEnabled and CacheType will be automatically set by computed properties
                            return; // Successfully parsed, don't need to check legacy
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Failed to parse REDIS_URL, will fall back to legacy configuration");
                            // Don't fail startup over this - fall through to legacy check
                        }
                    }
                }
                
                // Fall back to legacy connection string if REDIS_URL not provided or failed
                if (!string.IsNullOrEmpty(legacyConnectionString))
                {
                    options.RedisConnectionString = legacyConnectionString;
                }
            });

            // Register the cache service implementation
            services.AddSingleton<ICacheService, CacheService>();

            return services;
        }
    }
}
