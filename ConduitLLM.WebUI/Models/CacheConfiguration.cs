namespace ConduitLLM.WebUI.Models
{
    /// <summary>
    /// Represents the complete cache configuration as stored in the backend
    /// </summary>
    public class CacheConfiguration
    {
        /// <summary>
        /// Whether caching is enabled
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// The type of cache (Memory or Redis)
        /// </summary>
        public string CacheType { get; set; } = string.Empty;

        /// <summary>
        /// Default expiration time in minutes for cached items
        /// </summary>
        public int DefaultExpirationMinutes { get; set; }

        /// <summary>
        /// Maximum number of items to store in cache
        /// </summary>
        public int MaxCacheItems { get; set; }

        /// <summary>
        /// Redis connection string (when using Redis cache)
        /// </summary>
        public string? RedisConnectionString { get; set; }

        /// <summary>
        /// Redis instance name prefix (when using Redis cache)
        /// </summary>
        public string? RedisInstanceName { get; set; }

        /// <summary>
        /// Whether to include model name in cache key
        /// </summary>
        public bool IncludeModelInKey { get; set; }

        /// <summary>
        /// Whether to include provider name in cache key
        /// </summary>
        public bool IncludeProviderInKey { get; set; }

        /// <summary>
        /// Whether to include API key in cache key
        /// </summary>
        public bool IncludeApiKeyInKey { get; set; }

        /// <summary>
        /// Whether to include temperature in cache key
        /// </summary>
        public bool IncludeTemperatureInKey { get; set; }

        /// <summary>
        /// Whether to include max tokens in cache key
        /// </summary>
        public bool IncludeMaxTokensInKey { get; set; }

        /// <summary>
        /// Whether to include top P in cache key
        /// </summary>
        public bool IncludeTopPInKey { get; set; }
    }
}