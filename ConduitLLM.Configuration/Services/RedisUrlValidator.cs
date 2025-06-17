using System;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Services
{
    /// <summary>
    /// Validates Redis URL format and logs warnings for invalid URLs
    /// </summary>
    public static class RedisUrlValidator
    {
        private static readonly Regex RedisUrlRegex = new Regex(
            @"^rediss?://(?:(?<username>[^:@]+)?:?(?<password>[^@]+)?@)?(?<host>[^:]+)(?::(?<port>\d+))?$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Validates a Redis URL and logs appropriate warnings
        /// </summary>
        /// <param name="redisUrl">The Redis URL to validate</param>
        /// <param name="logger">Logger for warnings</param>
        /// <param name="serviceName">Name of the service for logging context</param>
        /// <returns>True if valid, false otherwise</returns>
        public static bool ValidateAndLog(string? redisUrl, ILogger logger, string serviceName)
        {
            if (string.IsNullOrWhiteSpace(redisUrl))
            {
                return false;
            }

            try
            {
                // Validate the URL format first
                if (!RedisUrlRegex.IsMatch(redisUrl))
                {
                    logger.LogWarning(
                        "{ServiceName}: Redis URL format appears invalid. Expected format: redis://[username]:[password]@hostname:port. URL: {RedisUrl}",
                        serviceName,
                        SanitizeUrlForLogging(redisUrl));
                    return false;
                }
                
                // Try to parse it
                var parsed = Utilities.RedisUrlParser.ParseRedisUrl(redisUrl);

                // Check for common issues
                if (redisUrl.Contains(" "))
                {
                    logger.LogWarning(
                        "{ServiceName}: Redis URL contains spaces. This is likely invalid. URL: {RedisUrl}",
                        serviceName,
                        SanitizeUrlForLogging(redisUrl));
                    return false;
                }

                // Check for missing port (common mistake)
                if (!redisUrl.Contains(":637") && !redisUrl.Contains(":638") && !redisUrl.Contains(":"))
                {
                    logger.LogWarning(
                        "{ServiceName}: Redis URL appears to be missing port number. Default Redis port is 6379. URL: {RedisUrl}",
                        serviceName,
                        SanitizeUrlForLogging(redisUrl));
                }

                logger.LogInformation(
                    "{ServiceName}: Redis URL validated successfully",
                    serviceName);
                
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "{ServiceName}: Failed to validate Redis URL. URL: {RedisUrl}",
                    serviceName,
                    SanitizeUrlForLogging(redisUrl));
                return false;
            }
        }

        /// <summary>
        /// Sanitizes a Redis URL for safe logging by masking sensitive information
        /// </summary>
        /// <param name="redisUrl">The URL to sanitize</param>
        /// <returns>Sanitized URL safe for logging</returns>
        private static string SanitizeUrlForLogging(string redisUrl)
        {
            if (string.IsNullOrWhiteSpace(redisUrl))
            {
                return "(empty)";
            }

            try
            {
                // Replace password with asterisks
                var match = RedisUrlRegex.Match(redisUrl);
                if (match.Success && match.Groups["password"].Success)
                {
                    var password = match.Groups["password"].Value;
                    return redisUrl.Replace(password, "****");
                }

                // If no password found, return as-is (already safe)
                return redisUrl;
            }
            catch
            {
                // If any error, just mask everything after ://
                var protocolIndex = redisUrl.IndexOf("://", StringComparison.OrdinalIgnoreCase);
                if (protocolIndex > 0)
                {
                    return redisUrl.Substring(0, protocolIndex + 3) + "****";
                }
                
                return "(invalid)";
            }
        }
    }
}