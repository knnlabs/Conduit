using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConduitLLM.WebUI.Data.Entities
{
    /// <summary>
    /// Database entity representing a fallback configuration for model routing
    /// </summary>
    public class FallbackConfigurationEntity
    {
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
        /// The fallback model deployments for this configuration
        /// </summary>
        public virtual ICollection<FallbackModelMappingEntity> FallbackMappings { get; set; } = new List<FallbackModelMappingEntity>();
    }
    
    /// <summary>
    /// Database entity representing a mapping between fallback configurations and model deployments
    /// </summary>
    public class FallbackModelMappingEntity
    {
        [Key]
        public int Id { get; set; }
        
        /// <summary>
        /// The ID of the fallback configuration
        /// </summary>
        public Guid FallbackConfigurationId { get; set; }
        
        /// <summary>
        /// Navigation property to the fallback configuration
        /// </summary>
        public virtual FallbackConfigurationEntity? FallbackConfiguration { get; set; }
        
        /// <summary>
        /// The ID of the model deployment to use as a fallback
        /// </summary>
        [Required]
        public Guid ModelDeploymentId { get; set; }
        
        /// <summary>
        /// The order in which this fallback should be tried (lower values are tried first)
        /// </summary>
        public int Order { get; set; }
    }
}
