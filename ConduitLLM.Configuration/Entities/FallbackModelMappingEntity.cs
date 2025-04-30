using System;
using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.Entities
{
    /// <summary>
    /// Database entity representing a mapping between fallback configurations and model deployments.
    /// This defines the ordered list of fallback models to try when a primary model fails.
    /// </summary>
    public class FallbackModelMappingEntity
    {
        /// <summary>
        /// Primary key for the fallback model mapping
        /// </summary>
        [Key]
        public int Id { get; set; }
        
        /// <summary>
        /// The ID of the fallback configuration this mapping belongs to
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
