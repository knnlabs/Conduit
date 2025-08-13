using System;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Extensions
{
    /// <summary>
    /// Helper class for logging deprecation warnings for old environment variables
    /// </summary>
    public static class DeprecationWarnings
    {
        /// <summary>
        /// Logs deprecation warnings if old environment variables are being used
        /// </summary>
        /// <param name="logger">The logger to use for warnings</param>
        public static void LogEnvironmentVariableDeprecations(ILogger logger)
        {
            // Check for deprecated Redis configuration
            var oldRedisConnectionString = Environment.GetEnvironmentVariable("CONDUIT_REDIS_CONNECTION_STRING");
            var newRedisUrl = Environment.GetEnvironmentVariable("REDIS_URL");
            
            if (!string.IsNullOrEmpty(oldRedisConnectionString) && string.IsNullOrEmpty(newRedisUrl))
            {
                logger.LogWarning(
                    "DEPRECATION WARNING: Environment variable 'CONDUIT_REDIS_CONNECTION_STRING' is deprecated. " +
                    "Please use 'REDIS_URL' instead with format: redis://[username]:[password]@hostname:port. " +
                    "The old variable will be removed in a future version.");
            }

            // Check for deprecated cache enabled/type
            var cacheEnabled = Environment.GetEnvironmentVariable("CONDUIT_CACHE_ENABLED");
            var cacheType = Environment.GetEnvironmentVariable("CONDUIT_CACHE_TYPE");
            
            if (!string.IsNullOrEmpty(cacheEnabled) || !string.IsNullOrEmpty(cacheType))
            {
                logger.LogWarning(
                    "DEPRECATION WARNING: Environment variables 'CONDUIT_CACHE_ENABLED' and 'CONDUIT_CACHE_TYPE' are deprecated. " +
                    "Cache is now automatically enabled when REDIS_URL is provided. " +
                    "These variables will be removed in a future version.");
            }

            // Check for deprecated master key
            var oldMasterKey = Environment.GetEnvironmentVariable("AdminApi__MasterKey");
            var newMasterKey = Environment.GetEnvironmentVariable("CONDUIT_API_TO_API_BACKEND_AUTH_KEY");
            
            if (!string.IsNullOrEmpty(oldMasterKey) && string.IsNullOrEmpty(newMasterKey))
            {
                logger.LogWarning(
                    "DEPRECATION WARNING: Configuration key 'AdminApi__MasterKey' is deprecated. " +
                    "Please use environment variable 'CONDUIT_API_TO_API_BACKEND_AUTH_KEY' instead. " +
                    "The old configuration key will be removed in a future version.");
            }
        }

        /// <summary>
        /// Gets a summary of deprecated environment variables in use
        /// </summary>
        /// <returns>A summary message or null if no deprecated variables are in use</returns>
        public static string? GetDeprecationSummary()
        {
            var deprecatedVars = new List<string>();

            var oldRedisConnectionString = Environment.GetEnvironmentVariable("CONDUIT_REDIS_CONNECTION_STRING");
            var newRedisUrl = Environment.GetEnvironmentVariable("REDIS_URL");
            if (!string.IsNullOrEmpty(oldRedisConnectionString) && string.IsNullOrEmpty(newRedisUrl))
            {
                deprecatedVars.Add("CONDUIT_REDIS_CONNECTION_STRING (use REDIS_URL)");
            }

            var cacheEnabled = Environment.GetEnvironmentVariable("CONDUIT_CACHE_ENABLED");
            if (!string.IsNullOrEmpty(cacheEnabled))
            {
                deprecatedVars.Add("CONDUIT_CACHE_ENABLED (no longer needed with REDIS_URL)");
            }

            var cacheType = Environment.GetEnvironmentVariable("CONDUIT_CACHE_TYPE");
            if (!string.IsNullOrEmpty(cacheType))
            {
                deprecatedVars.Add("CONDUIT_CACHE_TYPE (no longer needed with REDIS_URL)");
            }

            var oldMasterKey = Environment.GetEnvironmentVariable("AdminApi__MasterKey");
            var newMasterKey = Environment.GetEnvironmentVariable("CONDUIT_API_TO_API_BACKEND_AUTH_KEY");
            if (!string.IsNullOrEmpty(oldMasterKey) && string.IsNullOrEmpty(newMasterKey))
            {
                deprecatedVars.Add("AdminApi__MasterKey (use CONDUIT_API_TO_API_BACKEND_AUTH_KEY)");
            }

            if (deprecatedVars.Count() == 0)
            {
                return null;
            }

            return $"The following deprecated environment variables are in use: {string.Join(", ", deprecatedVars)}. " +
                   "Please see docs/MIGRATION_ENV_VARS.md for migration instructions.";
        }
    }
}