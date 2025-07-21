using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.Models
{
    /// <summary>
    /// Configuration for a cache region.
    /// </summary>
    public class CacheRegionConfig
    {
        /// <summary>
        /// Gets or sets the cache region name.
        /// </summary>
        public string Region { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether caching is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the default time-to-live for entries.
        /// </summary>
        public TimeSpan? DefaultTTL { get; set; }

        /// <summary>
        /// Gets or sets the maximum time-to-live for entries.
        /// </summary>
        public TimeSpan? MaxTTL { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of entries allowed.
        /// </summary>
        public long? MaxEntries { get; set; }

        /// <summary>
        /// Gets or sets the maximum memory size in bytes.
        /// </summary>
        public long? MaxMemoryBytes { get; set; }

        /// <summary>
        /// Gets or sets the priority level (0-100).
        /// </summary>
        public int Priority { get; set; } = 50;

        /// <summary>
        /// Gets or sets the eviction policy.
        /// </summary>
        public string EvictionPolicy { get; set; } = "LRU";

        /// <summary>
        /// Gets or sets whether to use memory cache.
        /// </summary>
        public bool UseMemoryCache { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to use distributed cache.
        /// </summary>
        public bool UseDistributedCache { get; set; } = false;

        /// <summary>
        /// Gets or sets whether detailed statistics are enabled.
        /// </summary>
        public bool EnableDetailedStats { get; set; } = true;

        /// <summary>
        /// Gets or sets whether compression is enabled.
        /// </summary>
        public bool EnableCompression { get; set; } = false;

        /// <summary>
        /// Gets or sets the compression threshold in bytes.
        /// </summary>
        public long? CompressionThresholdBytes { get; set; }

        /// <summary>
        /// Gets or sets extended properties.
        /// </summary>
        public Dictionary<string, object>? ExtendedProperties { get; set; }
    }

    /// <summary>
    /// Known cache regions in the system.
    /// </summary>
    public static class CacheRegions
    {
        public const string VirtualKeys = "VirtualKeys";
        public const string RateLimits = "RateLimits";
        public const string ProviderHealth = "ProviderHealth";
        public const string ModelMetadata = "ModelMetadata";
        public const string AuthTokens = "AuthTokens";
        public const string IpFilters = "IpFilters";
        public const string AsyncTasks = "AsyncTasks";
        public const string ProviderResponses = "ProviderResponses";
        public const string Embeddings = "Embeddings";
        public const string GlobalSettings = "GlobalSettings";
        public const string ProviderCredentials = "ProviderCredentials";
        public const string ModelCosts = "ModelCosts";
        public const string AudioStreams = "AudioStreams";
        public const string Monitoring = "Monitoring";
        public const string Default = "Default";

        /// <summary>
        /// Gets all known cache regions.
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
            ProviderCredentials,
            ModelCosts,
            AudioStreams,
            Monitoring,
            Default
        };
    }
}