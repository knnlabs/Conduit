using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConduitLLM.Configuration.Entities
{
    /// <summary>
    /// Database entity representing a model deployment that can be used by the Conduit router
    /// </summary>
    public class ModelDeploymentEntity
    {
        /// <summary>
        /// Unique identifier for the model deployment
        /// </summary>
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        /// <summary>
        /// The name of the model (e.g., gpt-4, claude-3-opus)
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string ModelName { get; set; } = string.Empty;
        
        /// <summary>
        /// The name of the provider for this model (e.g., OpenAI, Anthropic)
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string ProviderName { get; set; } = string.Empty;
        
        /// <summary>
        /// Weight for random selection strategy (higher values increase selection probability)
        /// </summary>
        public int Weight { get; set; } = 1;
        
        /// <summary>
        /// Whether health checking is enabled for this deployment
        /// </summary>
        public bool HealthCheckEnabled { get; set; } = true;
        
        /// <summary>
        /// Whether this deployment is enabled and available for routing
        /// </summary>
        public bool IsEnabled { get; set; } = true;
        
        /// <summary>
        /// Maximum requests per minute for this deployment
        /// </summary>
        public int? RPM { get; set; }
        
        /// <summary>
        /// Maximum tokens per minute for this deployment
        /// </summary>
        public int? TPM { get; set; }
        
        /// <summary>
        /// Cost per 1000 input tokens
        /// </summary>
        [Column(TypeName = "decimal(18, 8)")]
        public decimal? InputTokenCostPer1K { get; set; }
        
        /// <summary>
        /// Cost per 1000 output tokens
        /// </summary>
        [Column(TypeName = "decimal(18, 8)")]
        public decimal? OutputTokenCostPer1K { get; set; }
        
        /// <summary>
        /// Priority of this deployment (lower values are higher priority)
        /// </summary>
        public int Priority { get; set; } = 1;
        
        /// <summary>
        /// Health status of this deployment
        /// </summary>
        public bool IsHealthy { get; set; } = true;
        
        /// <summary>
        /// Foreign key to the router configuration
        /// </summary>
        public int RouterConfigId { get; set; }
        
        /// <summary>
        /// Navigation property to the router configuration
        /// </summary>
        public virtual RouterConfigEntity? RouterConfig { get; set; }
        
        /// <summary>
        /// When the deployment was last updated
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
