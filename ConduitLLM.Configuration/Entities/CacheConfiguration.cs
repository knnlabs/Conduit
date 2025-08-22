using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.Entities
{
    /// <summary>
    /// Represents cache configuration settings for a specific region.
    /// </summary>
    public class CacheConfiguration
    {
        /// <summary>
        /// Gets or sets the unique identifier.
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the cache region.
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Region { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether caching is enabled for this region.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the default TTL in seconds.
        /// </summary>
        public int? DefaultTtlSeconds { get; set; }

        /// <summary>
        /// Gets or sets the maximum TTL in seconds.
        /// </summary>
        public int? MaxTtlSeconds { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of entries.
        /// </summary>
        public long? MaxEntries { get; set; }

        /// <summary>
        /// Gets or sets the maximum memory size in bytes.
        /// </summary>
        public long? MaxMemoryBytes { get; set; }

        /// <summary>
        /// Gets or sets the eviction policy.
        /// </summary>
        [MaxLength(20)]
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
        /// Gets or sets whether compression is enabled.
        /// </summary>
        public bool EnableCompression { get; set; } = false;

        /// <summary>
        /// Gets or sets the compression threshold in bytes.
        /// </summary>
        public long? CompressionThresholdBytes { get; set; }

        /// <summary>
        /// Gets or sets the priority level (0-100).
        /// </summary>
        [Range(0, 100)]
        public int Priority { get; set; } = 50;

        /// <summary>
        /// Gets or sets whether detailed statistics are enabled.
        /// </summary>
        public bool EnableDetailedStats { get; set; } = true;

        /// <summary>
        /// Gets or sets additional configuration as JSON.
        /// </summary>
        public string? ExtendedConfig { get; set; }

        /// <summary>
        /// Gets or sets when this configuration was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets when this configuration was last updated.
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets who created this configuration.
        /// </summary>
        [MaxLength(100)]
        public string? CreatedBy { get; set; }

        /// <summary>
        /// Gets or sets who last updated this configuration.
        /// </summary>
        [MaxLength(100)]
        public string? UpdatedBy { get; set; }

        /// <summary>
        /// Gets or sets the version number for optimistic concurrency.
        /// </summary>
        [Timestamp]
        public byte[]? Version { get; set; }

        /// <summary>
        /// Gets or sets whether this is the active configuration.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Gets or sets notes or description for this configuration.
        /// </summary>
        [MaxLength(500)]
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Represents an audit log entry for cache configuration changes.
    /// </summary>
    public class CacheConfigurationAudit
    {
        /// <summary>
        /// Gets or sets the unique identifier.
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the cache region.
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Region { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the action performed.
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the old configuration as JSON.
        /// </summary>
        public string? OldConfigJson { get; set; }

        /// <summary>
        /// Gets or sets the new configuration as JSON.
        /// </summary>
        public string? NewConfigJson { get; set; }

        /// <summary>
        /// Gets or sets the reason for the change.
        /// </summary>
        [MaxLength(500)]
        public string? Reason { get; set; }

        /// <summary>
        /// Gets or sets who made the change.
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string ChangedBy { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets when the change was made.
        /// </summary>
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the source of the change (API, UI, System).
        /// </summary>
        [MaxLength(50)]
        public string? ChangeSource { get; set; }

        /// <summary>
        /// Gets or sets whether the change was successful.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Gets or sets any error message if the change failed.
        /// </summary>
        [MaxLength(1000)]
        public string? ErrorMessage { get; set; }
    }
}