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
        /// Default sliding expiration time in minutes
        /// </summary>
        public int DefaultSlidingExpirationMinutes { get; set; } = 20; // 20 minutes default

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
        /// Whether to include the model name in the cache key
        /// </summary>
        public bool IncludeModelInKey { get; set; } = true;

        /// <summary>
        /// Whether to include the provider name in the cache key
        /// </summary>
        public bool IncludeProviderInKey { get; set; } = true;

        /// <summary>
        /// Whether to include the API key in the cache key
        /// </summary>
        public bool IncludeApiKeyInKey { get; set; } = false;

        /// <summary>
        /// Whether to include the temperature in the cache key
        /// </summary>
        public bool IncludeTemperatureInKey { get; set; } = true;

        /// <summary>
        /// Whether to include the max tokens in the cache key
        /// </summary>
        public bool IncludeMaxTokensInKey { get; set; } = false;

        /// <summary>
        /// Whether to include the Top-P in the cache key
        /// </summary>
        public bool IncludeTopPInKey { get; set; } = false;

        /// <summary>
        /// The hash algorithm to use for cache keys
        /// </summary>
        public string HashAlgorithm { get; set; } = "MD5";

        /// <summary>
        /// Model-specific caching rules
        /// </summary>
        public List<ModelCacheRule> ModelSpecificRules { get; set; } = new List<ModelCacheRule>();

        /// <summary>
        /// Default expiration time in minutes
        /// </summary>
        public int DefaultExpirationMinutes { get; set; } = 60;

        /// <summary>
        /// Gets the default absolute expiration time as TimeSpan
        /// </summary>
        public TimeSpan? DefaultAbsoluteExpiration =>
            UseDefaultExpirationTimes && DefaultAbsoluteExpirationMinutes > 0
                ? TimeSpan.FromMinutes(DefaultAbsoluteExpirationMinutes)
                : null;

        /// <summary>
        /// Gets the default sliding expiration time as TimeSpan
        /// </summary>
        public TimeSpan? DefaultSlidingExpiration =>
            UseDefaultExpirationTimes && DefaultSlidingExpirationMinutes > 0
                ? TimeSpan.FromMinutes(DefaultSlidingExpirationMinutes)
                : null;
    }

    /// <summary>
    /// Caching behavior for models
    /// </summary>
    public enum CacheBehavior
    {
        /// <summary>
        /// Use the default cache settings
        /// </summary>
        Default = 0,

        /// <summary>
        /// Always cache responses for this model
        /// </summary>
        Always = 1,

        /// <summary>
        /// Never cache responses for this model
        /// </summary>
        Never = 2
    }

    /// <summary>
    /// Model-specific cache rule
    /// </summary>
    public class ModelCacheRule
    {
        /// <summary>
        /// Pattern to match model names against
        /// </summary>
        public string ModelNamePattern { get; set; } = string.Empty;

        /// <summary>
        /// Caching behavior for models matching the pattern
        /// </summary>
        public CacheBehavior CacheBehavior { get; set; } = CacheBehavior.Default;

        /// <summary>
        /// Custom expiration time in minutes (overrides default)
        /// </summary>
        public int? ExpirationMinutes { get; set; }
    }
}
