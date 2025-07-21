using System;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Attributes
{
    /// <summary>
    /// Attribute to mark a class or method that uses a specific cache region.
    /// Used for automatic discovery and registration of cache regions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = true)]
    public class CacheRegionAttribute : Attribute
    {
        /// <summary>
        /// The cache region used.
        /// </summary>
        public CacheRegion Region { get; }

        /// <summary>
        /// Optional description of how the cache is used.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Suggested TTL for entries in this usage context.
        /// Use -1 to indicate no suggestion.
        /// </summary>
        public int SuggestedTtlSeconds { get; set; } = -1;

        /// <summary>
        /// Whether this usage requires distributed cache.
        /// </summary>
        public bool RequiresDistributed { get; set; }

        /// <summary>
        /// Priority for this cache usage (higher = more important).
        /// </summary>
        public int Priority { get; set; } = 50;

        /// <summary>
        /// Initializes a new instance of the CacheRegionAttribute.
        /// </summary>
        /// <param name="region">The cache region used.</param>
        public CacheRegionAttribute(CacheRegion region)
        {
            Region = region;
        }
    }

    /// <summary>
    /// Attribute to mark a custom cache region usage.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = true)]
    public class CustomCacheRegionAttribute : Attribute
    {
        /// <summary>
        /// Name of the custom cache region.
        /// </summary>
        public string RegionName { get; }

        /// <summary>
        /// Description of the cache region.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Default TTL in seconds.
        /// </summary>
        public int DefaultTtlSeconds { get; set; } = 900; // 15 minutes

        /// <summary>
        /// Maximum TTL in seconds.
        /// Use -1 to indicate no maximum.
        /// </summary>
        public int MaxTtlSeconds { get; set; } = -1;

        /// <summary>
        /// Whether to use distributed cache.
        /// </summary>
        public bool UseDistributed { get; set; } = true;

        /// <summary>
        /// Whether to use memory cache.
        /// </summary>
        public bool UseMemory { get; set; } = true;

        /// <summary>
        /// Eviction policy name.
        /// </summary>
        public string EvictionPolicy { get; set; } = "LRU";

        /// <summary>
        /// Priority for this region.
        /// </summary>
        public int Priority { get; set; } = 50;

        /// <summary>
        /// Initializes a new instance of the CustomCacheRegionAttribute.
        /// </summary>
        /// <param name="regionName">Name of the custom cache region.</param>
        public CustomCacheRegionAttribute(string regionName)
        {
            RegionName = regionName ?? throw new ArgumentNullException(nameof(regionName));
        }
    }

    /// <summary>
    /// Attribute to indicate that a class manages its own cache configuration.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class CacheConfigurationProviderAttribute : Attribute
    {
        /// <summary>
        /// Method name that provides cache configuration.
        /// Method should be static and return CacheRegionConfig or IEnumerable<CacheRegionConfig>.
        /// </summary>
        public string? ConfigurationMethodName { get; set; }

        /// <summary>
        /// Property name that provides cache configuration.
        /// Property should be static and return CacheRegionConfig or IEnumerable<CacheRegionConfig>.
        /// </summary>
        public string? ConfigurationPropertyName { get; set; }
    }

    /// <summary>
    /// Marks a cache region as dependent on another region.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class CacheDependencyAttribute : Attribute
    {
        /// <summary>
        /// The cache region this depends on.
        /// </summary>
        public CacheRegion DependsOn { get; }

        /// <summary>
        /// Type of dependency.
        /// </summary>
        public CacheDependencyType DependencyType { get; set; } = CacheDependencyType.Invalidation;

        /// <summary>
        /// Initializes a new instance of the CacheDependencyAttribute.
        /// </summary>
        /// <param name="dependsOn">The cache region this depends on.</param>
        public CacheDependencyAttribute(CacheRegion dependsOn)
        {
            DependsOn = dependsOn;
        }
    }

    /// <summary>
    /// Type of cache dependency.
    /// </summary>
    public enum CacheDependencyType
    {
        /// <summary>
        /// When the dependency is invalidated, this cache should also be invalidated.
        /// </summary>
        Invalidation,

        /// <summary>
        /// This cache reads from the dependency cache.
        /// </summary>
        Read,

        /// <summary>
        /// This cache writes to the dependency cache.
        /// </summary>
        Write,

        /// <summary>
        /// Changes in the dependency trigger refresh of this cache.
        /// </summary>
        Refresh
    }
}