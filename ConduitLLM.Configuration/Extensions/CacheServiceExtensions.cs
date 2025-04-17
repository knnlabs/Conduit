using System;

using ConduitLLM.Configuration.Options;
using ConduitLLM.Configuration.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
            
            // Register the cache service implementation
            services.AddSingleton<ICacheService, CacheService>();
            
            return services;
        }
    }
}
