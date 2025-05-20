using System;
using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Data transfer object for model cost information
    /// </summary>
    public class ModelCostDto
    {
        /// <summary>
        /// Unique identifier for the model cost entry
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Model identification pattern, which can include wildcards
        /// </summary>
        /// <remarks>
        /// Examples: "openai/gpt-4o", "anthropic.claude-3*", "*-embedding-*"
        /// </remarks>
        [Required]
        [MaxLength(255)]
        public string ModelIdPattern { get; set; } = string.Empty;

        /// <summary>
        /// Cost per input token for chat/completion requests in USD
        /// </summary>
        [Range(0, double.MaxValue)]
        public decimal InputTokenCost { get; set; } = 0;

        /// <summary>
        /// Cost per output token for chat/completion requests in USD
        /// </summary>
        [Range(0, double.MaxValue)]
        public decimal OutputTokenCost { get; set; } = 0;

        /// <summary>
        /// Cost per token for embedding requests in USD, if applicable
        /// </summary>
        public decimal? EmbeddingTokenCost { get; set; }

        /// <summary>
        /// Cost per image for image generation requests in USD, if applicable
        /// </summary>
        public decimal? ImageCostPerImage { get; set; }

        /// <summary>
        /// Creation timestamp of this cost record
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Last update timestamp of this cost record
        /// </summary>
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Optional description for this model cost entry (for backward compatibility)
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Optional priority value for this model cost entry (for backward compatibility)
        /// </summary>
        public int Priority { get; set; }
    }

    /// <summary>
    /// Data transfer object for creating a model cost entry
    /// </summary>
    public class CreateModelCostDto
    {
        /// <summary>
        /// Model identification pattern, which can include wildcards
        /// </summary>
        /// <remarks>
        /// Examples: "openai/gpt-4o", "anthropic.claude-3*", "*-embedding-*"
        /// </remarks>
        [Required]
        [MaxLength(255)]
        public string ModelIdPattern { get; set; } = string.Empty;

        /// <summary>
        /// Cost per input token for chat/completion requests in USD
        /// </summary>
        [Range(0, double.MaxValue)]
        public decimal InputTokenCost { get; set; } = 0;

        /// <summary>
        /// Cost per output token for chat/completion requests in USD
        /// </summary>
        [Range(0, double.MaxValue)]
        public decimal OutputTokenCost { get; set; } = 0;

        /// <summary>
        /// Cost per token for embedding requests in USD, if applicable
        /// </summary>
        public decimal? EmbeddingTokenCost { get; set; }

        /// <summary>
        /// Cost per image for image generation requests in USD, if applicable
        /// </summary>
        public decimal? ImageCostPerImage { get; set; }
    }

    /// <summary>
    /// Data transfer object for updating a model cost entry
    /// </summary>
    public class UpdateModelCostDto
    {
        /// <summary>
        /// Unique identifier for the model cost entry
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Model identification pattern, which can include wildcards
        /// </summary>
        /// <remarks>
        /// Examples: "openai/gpt-4o", "anthropic.claude-3*", "*-embedding-*"
        /// </remarks>
        [Required]
        [MaxLength(255)]
        public string ModelIdPattern { get; set; } = string.Empty;

        /// <summary>
        /// Cost per input token for chat/completion requests in USD
        /// </summary>
        [Range(0, double.MaxValue)]
        public decimal InputTokenCost { get; set; } = 0;

        /// <summary>
        /// Cost per output token for chat/completion requests in USD
        /// </summary>
        [Range(0, double.MaxValue)]
        public decimal OutputTokenCost { get; set; } = 0;

        /// <summary>
        /// Cost per token for embedding requests in USD, if applicable
        /// </summary>
        public decimal? EmbeddingTokenCost { get; set; }

        /// <summary>
        /// Cost per image for image generation requests in USD, if applicable
        /// </summary>
        public decimal? ImageCostPerImage { get; set; }
    }

    /// <summary>
    /// Data transfer object for model cost overview data used in dashboards
    /// </summary>
    public class ModelCostOverviewDto
    {
        /// <summary>
        /// Model name or pattern
        /// </summary>
        public string Model { get; set; } = string.Empty;
        
        /// <summary>
        /// Number of requests for this model
        /// </summary>
        public int RequestCount { get; set; }
        
        /// <summary>
        /// Total cost for this model in USD
        /// </summary>
        public decimal TotalCost { get; set; }
        
        /// <summary>
        /// Total input tokens processed
        /// </summary>
        public long InputTokens { get; set; }
        
        /// <summary>
        /// Total output tokens generated
        /// </summary>
        public long OutputTokens { get; set; }
    }
}