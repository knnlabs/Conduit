using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Base class for Redis-backed webhook services.
    /// Consolidates shared constructor pattern and URL hashing.
    /// </summary>
    public abstract class RedisWebhookServiceBase
    {
        protected readonly IConnectionMultiplexer Redis;
        protected readonly ILogger Logger;

        protected RedisWebhookServiceBase(
            IConnectionMultiplexer redis,
            ILogger logger)
        {
            Redis = redis ?? throw new ArgumentNullException(nameof(redis));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates a consistent, URL-safe hash for use as a Redis key component.
        /// </summary>
        protected static string GetUrlHash(string webhookUrl)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(webhookUrl));
            return Convert.ToBase64String(hashBytes).Replace("/", "-").Replace("+", "_").Substring(0, 16);
        }
    }
}
