namespace ConduitLLM.Configuration.Models
{
    /// <summary>
    /// Configuration for a cache region.
    /// </summary>
    /// <summary>
    /// Defines the configuration for a specific cache region. Each region can have its own set of rules,
    /// such as time-to-live (TTL), memory limits, and eviction policies. This allows for granular control
    /// over caching behavior for different types of data throughout the application.
    /// </summary>
    /// <remarks>
    /// This class is typically used with dependency injection to configure caching services.
    /// The settings can be populated from a configuration file (e.g., appsettings.json),
    /// allowing for flexible cache management without changing the code.
    /// </remarks>
    public class CacheRegionConfig
    {
        /// <summary>
        /// Gets or sets the unique name for the cache region.
        /// This name is used to identify and retrieve the configuration for a specific cache.
        /// </summary>
        /// <example>"AuthTokens", "ModelMetadata"</example>
        public string Region { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether this cache region is active.
        /// If set to false, any attempts to cache data in this region will be ignored.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the default time-to-live (TTL) for cache entries in this region.
        /// If not specified, a system-wide default may be used.
        /// </summary>
        /// <remarks>
        /// This value determines how long an item will remain in the cache before it is automatically evicted.
        /// </remarks>
        public TimeSpan? DefaultTTL { get; set; }

        /// <summary>
        /// Gets or sets the maximum time-to-live (TTL) for cache entries in this region.
        /// This can be used to enforce an upper limit on cache duration, even if a longer TTL is requested.
        /// </summary>
        public TimeSpan? MaxTTL { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of entries that can be stored in this cache region.
        /// When this limit is reached, the cache will evict items based on the specified eviction policy.
        /// </summary>
        public long? MaxEntries { get; set; }

        /// <summary>
        /// Gets or sets the maximum memory size in bytes that this cache region can consume.
        /// When this limit is reached, the cache will evict items to free up memory.
        /// </summary>
        public long? MaxMemoryBytes { get; set; }

        /// <summary>
        /// Gets or sets the priority of this cache region, typically on a scale of 0-100.
        /// Higher priority regions may be less likely to have their items evicted during memory pressure.
        /// </summary>
        public int Priority { get; set; } = 50;

        /// <summary>
        /// Gets or sets the eviction policy to use when the cache reaches its size or memory limit.
        /// Common policies include "LRU" (Least Recently Used) and "LFU" (Least Frequently Used).
        /// </summary>
        /// <example>"LRU", "LFU", "FIFO"</example>
        public string EvictionPolicy { get; set; } = "LRU";

        /// <summary>
        /// Gets or sets a value indicating whether to use an in-memory cache for this region.
        /// In-memory caches are fast but are local to a single application instance.
        /// </summary>
        public bool UseMemoryCache { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to use a distributed cache (e.g., Redis) for this region.
        /// Distributed caches can be shared across multiple application instances.
        /// </summary>
        public bool UseDistributedCache { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to collect detailed performance statistics for this cache region.
        /// Enabling this may have a minor performance impact.
        /// </summary>
        public bool EnableDetailedStats { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to compress cached items.
        /// This can save memory but adds CPU overhead for compression and decompression.
        /// </summary>
        public bool EnableCompression { get; set; } = false;

        /// <summary>
        /// Gets or sets the minimum size in bytes an item must be to be considered for compression.
        /// Items smaller than this threshold will not be compressed, even if compression is enabled.
        /// </summary>
        public long? CompressionThresholdBytes { get; set; }

        /// <summary>
        /// Gets or sets a dictionary for any custom or extended properties required by a specific cache implementation.
        /// This provides a flexible way to add provider-specific settings.
        /// </summary>
        public Dictionary<string, object>? ExtendedProperties { get; set; }
    }

    /// <summary>
    /// Provides a centralized list of well-known cache region names used throughout the application.
    /// Using these constants helps prevent typos and ensures consistency when referring to cache regions.
    /// </summary>
    /// <remarks>
    /// Each constant represents a logical partition of the cache, intended for a specific type of data.
    /// For example, `AuthTokens` is for caching authentication tokens, while `ModelMetadata` is for caching
    /// metadata about machine learning models.
    /// </remarks>
    public static class CacheRegions
    {
        /// <summary>Cache for virtual API keys and their mappings.</summary>
        public const string VirtualKeys = "VirtualKeys";
        /// <summary>Cache for tracking API rate limit counters.</summary>
        public const string RateLimits = "RateLimits";
        /// <summary>Cache for the health status of external providers.</summary>
        public const string ProviderHealth = "ProviderHealth";
        /// <summary>Cache for metadata about available AI/ML models.</summary>
        public const string ModelMetadata = "ModelMetadata";
        /// <summary>Cache for authentication and authorization tokens.</summary>
        public const string AuthTokens = "AuthTokens";
        /// <summary>Cache for IP filter lists and rules.</summary>
        public const string IpFilters = "IpFilters";
        /// <summary>Cache for the status and results of asynchronous tasks.</summary>
        public const string AsyncTasks = "AsyncTasks";
        /// <summary>Cache for responses from external providers to reduce redundant calls.</summary>
        public const string ProviderResponses = "ProviderResponses";
        /// <summary>Cache for text embeddings to speed up similarity searches.</summary>
        public const string Embeddings = "Embeddings";
        /// <summary>Cache for application-wide global settings.</summary>
        public const string GlobalSettings = "GlobalSettings";
        /// <summary>Cache for credentials used to access external providers.</summary>
        public const string Providers = "Providers";
        /// <summary>Cache for the cost information of different AI/ML models.</summary>
        public const string ModelCosts = "ModelCosts";
        /// <summary>Cache for audio stream data or metadata.</summary>
        public const string AudioStreams = "AudioStreams";
        /// <summary>Cache for monitoring and telemetry data.</summary>
        public const string Monitoring = "Monitoring";
        /// <summary>A default cache region for general-purpose caching.</summary>
        public const string Default = "Default";

        /// <summary>
        /// Gets an array containing all defined cache region names.
        /// This is useful for iterating over all regions, for example, to apply a configuration to all of them.
        /// </summary>
        public static string[] All => new[]
        {
            VirtualKeys,
            RateLimits,
            ProviderHealth,
            ModelMetadata,
            AuthTokens,
            IpFilters,
            AsyncTasks,
            ProviderResponses,
            Embeddings,
            GlobalSettings,
            Providers,
            ModelCosts,
            AudioStreams,
            Monitoring,
            Default
        };
    }
}