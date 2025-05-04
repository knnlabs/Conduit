using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.Entities
{
    /// <summary>
    /// Database entity representing a fallback configuration for model routing in Conduit.
    /// Defines which models should be used as fallbacks when a primary model fails.
    /// </summary>
    public class FallbackConfigurationEntity
    {
        /// <summary>
        /// Unique identifier for the fallback configuration
        /// </summary>
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        /// <summary>
        /// The ID of the primary model deployment that will fall back to others if it fails
        /// </summary>
        [Required]
        public Guid PrimaryModelDeploymentId { get; set; }
        
        /// <summary>
        /// Foreign key to the router configuration
        /// </summary>
        public int RouterConfigId { get; set; }
        
        /// <summary>
        /// Navigation property to the router configuration
        /// </summary>
        public virtual RouterConfigEntity? RouterConfig { get; set; }
        
        /// <summary>
        /// When the fallback configuration was last updated
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// When the configuration was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// When the configuration was last updated (alias for LastUpdated)
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Name of the configuration
        /// </summary>
        public string Name { get; set; } = "Default Fallback Configuration";
        
        /// <summary>
        /// Whether this fallback configuration is active
        /// </summary>
        public bool IsActive { get; set; } = false;
        
        /// <summary>
        /// The fallback model deployments for this configuration, ordered by preference
        /// </summary>
        public virtual ICollection<FallbackModelMappingEntity> FallbackMappings { get; set; } = new List<FallbackModelMappingEntity>();
    }
}
