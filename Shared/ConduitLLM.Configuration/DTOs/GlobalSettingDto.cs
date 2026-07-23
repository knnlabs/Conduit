using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Data transfer object for global settings
    /// </summary>
    public class GlobalSettingDto
    {
        /// <summary>
        /// Unique identifier for the setting
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Setting key
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Setting value
        /// </summary>
        [Required]
        [MaxLength(2000)]
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Optional description of the setting
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Date when the setting was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Date when the setting was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// Data transfer object for creating a global setting
    /// </summary>
    public class CreateGlobalSettingDto
    {
        /// <summary>
        /// Setting key
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Setting value
        /// </summary>
        [Required]
        [MaxLength(2000)]
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Optional description of the setting
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }
    }

    /// <summary>
    /// Data transfer object for updating a global setting
    /// </summary>
    public class UpdateGlobalSettingDto
    {
        [System.Text.Json.Serialization.JsonIgnore]
        public int Id { get; set; }
        /// <summary>
        /// Unique identifier for the setting
        /// </summary>
        /// <summary>
        /// Setting value
        /// </summary>
        [MaxLength(2000)]
        public string? Value { get; set; }

        /// <summary>
        /// Optional description of the setting
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }
    }

    /// <summary>
    /// Data transfer object for updating a global setting by key
    /// </summary>
    public class UpdateGlobalSettingByKeyDto
    {
        /// <summary>
        /// Setting key
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Setting value
        /// </summary>
        [Required]
        [MaxLength(2000)]
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Optional description of the setting
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }
    }

    /// <summary>
    /// Data transfer object for global settings cache statistics
    /// </summary>
    public class GlobalSettingCacheStatsDto
    {
        /// <summary>
        /// Number of settings currently in cache
        /// </summary>
        public int CacheSize { get; set; }

        /// <summary>
        /// Total number of cache hits
        /// </summary>
        public long CacheHits { get; set; }

        /// <summary>
        /// Total number of cache misses
        /// </summary>
        public long CacheMisses { get; set; }

        /// <summary>
        /// Total number of cache invalidations
        /// </summary>
        public long Invalidations { get; set; }

        /// <summary>
        /// Cache hit rate as a percentage (0-100)
        /// </summary>
        public double HitRate { get; set; }

        /// <summary>
        /// Timestamp of when the cache was last loaded from database
        /// </summary>
        public DateTime LastLoadTime { get; set; }

        /// <summary>
        /// List of all keys currently cached
        /// </summary>
        public List<string> CachedKeys { get; set; } = new();
    }
}
