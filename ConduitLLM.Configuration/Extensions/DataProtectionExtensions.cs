using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;

namespace ConduitLLM.Configuration.Extensions
{
    /// <summary>
    /// Extension methods for configuring Data Protection with Redis persistence
    /// </summary>
    public static class DataProtectionExtensions
    {
        /// <summary>
        /// Configures Data Protection to persist keys to Redis for distributed scenarios
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="redisConnectionString">Redis connection string</param>
        /// <param name="applicationName">The application name for key isolation</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddRedisDataProtection(
            this IServiceCollection services,
            string? redisConnectionString,
            string applicationName = "Conduit")
        {
            // If no Redis connection string is provided, fall back to default file system storage
            if (string.IsNullOrEmpty(redisConnectionString))
            {
                services.AddDataProtection()
                    .SetApplicationName(applicationName);
                
                using var serviceProvider = services.BuildServiceProvider();
                var logger = serviceProvider.GetService<ILogger<IDataProtectionBuilder>>();
                logger?.LogWarning("Redis connection string not provided. Data Protection keys will be stored in the file system. This may cause issues in distributed scenarios.");
                
                return services;
            }

            try
            {
                // Parse the connection string to handle different formats
                var connectionString = ParseRedisConnectionString(redisConnectionString);
                
                // Create Redis connection
                var redis = ConnectionMultiplexer.Connect(connectionString);
                
                // Configure Data Protection to use Redis
                services.AddDataProtection()
                    .PersistKeysToStackExchangeRedis(redis, $"{applicationName}-DataProtection-Keys")
                    .SetApplicationName(applicationName);
                
                // Register the Redis connection as a singleton for reuse
                services.AddSingleton<IConnectionMultiplexer>(redis);
                
                using var serviceProvider = services.BuildServiceProvider();
                var logger = serviceProvider.GetService<ILogger<IDataProtectionBuilder>>();
                logger?.LogInformation("Data Protection configured to use Redis for key persistence at {RedisEndpoint}", connectionString);
            }
            catch (Exception ex)
            {
                // If Redis connection fails, fall back to file system
                services.AddDataProtection()
                    .SetApplicationName(applicationName);
                
                using var serviceProvider = services.BuildServiceProvider();
                var logger = serviceProvider.GetService<ILogger<IDataProtectionBuilder>>();
                logger?.LogError(ex, "Failed to connect to Redis for Data Protection. Falling back to file system storage.");
            }
            
            return services;
        }

        /// <summary>
        /// Parses and normalizes Redis connection string formats
        /// </summary>
        /// <param name="connectionString">The connection string to parse</param>
        /// <returns>A normalized connection string</returns>
        private static string ParseRedisConnectionString(string connectionString)
        {
            // Handle simple host:port format
            if (!connectionString.Contains("=") && connectionString.Contains(":"))
            {
                return connectionString;
            }
            
            // Handle host only format (default port 6379)
            if (!connectionString.Contains(":") && !connectionString.Contains("="))
            {
                return $"{connectionString}:6379";
            }
            
            // Return as-is for full connection strings
            return connectionString;
        }
    }
}