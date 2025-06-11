using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.Entities
{
    /// <summary>
    /// Database entity representing router configuration for LLM routing
    /// </summary>
    public class RouterConfigEntity
    {
        /// <summary>
        /// Primary key for the router configuration
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Default routing strategy to use when not explicitly specified
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string DefaultRoutingStrategy { get; set; } = "simple";

        /// <summary>
        /// Maximum number of retries for a failed request
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Base delay in milliseconds between retries (for exponential backoff)
        /// </summary>
        public int RetryBaseDelayMs { get; set; } = 500;

        /// <summary>
        /// Maximum delay in milliseconds between retries
        /// </summary>
        public int RetryMaxDelayMs { get; set; } = 10000;

        /// <summary>
        /// Whether fallbacks are enabled
        /// </summary>
        public bool FallbacksEnabled { get; set; } = false;

        /// <summary>
        /// When the configuration was last updated
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether this configuration is active
        /// </summary>
        public bool IsActive { get; set; } = false;

        /// <summary>
        /// Name of the configuration (for display purposes)
        /// </summary>
        public string Name { get; set; } = "Default Configuration";

        /// <summary>
        /// When this configuration was last updated (alias for LastUpdated)
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When this configuration was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Model deployments associated with this configuration
        /// </summary>
        public virtual ICollection<ModelDeploymentEntity> ModelDeployments { get; set; } = new List<ModelDeploymentEntity>();

        /// <summary>
        /// Fallback configurations associated with this configuration
        /// </summary>
        public virtual ICollection<FallbackConfigurationEntity> FallbackConfigurations { get; set; } = new List<FallbackConfigurationEntity>();
    }
}
