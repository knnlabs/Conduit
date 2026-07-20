namespace ConduitLLM.Configuration.Options
{
    /// <summary>
    /// Configuration options for the cache service
    /// </summary>
    public class CacheOptions
    {
        /// <summary>
        /// Section name for configuration
        /// </summary>
        public const string SectionName = "Cache";

        private bool? _isEnabledOverride;
        private string? _cacheTypeOverride;

        /// <summary>
        /// Whether the cache is enabled
        /// Auto-enables when Redis is configured unless explicitly overridden
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabledOverride ?? !string.IsNullOrWhiteSpace(RedisConnectionString);
            set => _isEnabledOverride = value;
        }

        /// <summary>
        /// The type of cache to use
        /// Automatically set to "Redis" when Redis is configured unless explicitly overridden
        /// </summary>
        public string CacheType 
        { 
            get => _cacheTypeOverride ?? (!string.IsNullOrWhiteSpace(RedisConnectionString) ? "Redis" : "Memory");
            set => _cacheTypeOverride = value;
        }

        /// <summary>
        /// Default absolute expiration time in minutes
        /// </summary>
        public int DefaultAbsoluteExpirationMinutes { get; set; } = 60; // 1 hour default

        /// <summary>
        /// Whether to use default expiration times when not specified
        /// </summary>
        public bool UseDefaultExpirationTimes { get; set; } = true;

        /// <summary>
        /// Maximum number of items in the memory cache
        /// </summary>
        public int MaxCacheItems { get; set; } = 10000;

        /// <summary>
        /// Redis connection string (when Redis cache is used)
        /// </summary>
        public string RedisConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Redis instance name (when Redis cache is used)
        /// </summary>
        public string RedisInstanceName { get; set; } = "conduitllm-cache";

        /// <summary>
        /// Gets the default absolute expiration time as TimeSpan
        /// </summary>
        public TimeSpan? DefaultAbsoluteExpiration =>
            UseDefaultExpirationTimes && DefaultAbsoluteExpirationMinutes > 0
                ? TimeSpan.FromMinutes(DefaultAbsoluteExpirationMinutes)
                : null;
    }

}
