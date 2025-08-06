using System;
using System.Collections.Generic;
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
        /// User-friendly name for this cost configuration
        /// </summary>
        /// <remarks>
        /// Examples: "GPT-4 Standard Pricing", "Llama 3 Unified Cost", "Embedding Models - Ada"
        /// </remarks>
        [Required]
        [MaxLength(255)]
        public string CostName { get; set; } = string.Empty;

        /// <summary>
        /// List of model aliases that use this cost configuration
        /// </summary>
        /// <remarks>
        /// This is populated from the ModelCostMappings relationship.
        /// Shows which models are associated with this cost configuration.
        /// </remarks>
        public List<string> AssociatedModelAliases { get; set; } = new List<string>();

        /// <summary>
        /// Cost per million input tokens for chat/completion requests in USD
        /// </summary>
        [Range(0, double.MaxValue)]
        public decimal InputCostPerMillionTokens { get; set; } = 0;

        /// <summary>
        /// Cost per million output tokens for chat/completion requests in USD
        /// </summary>
        [Range(0, double.MaxValue)]
        public decimal OutputCostPerMillionTokens { get; set; } = 0;

        /// <summary>
        /// Cost per million tokens for embedding requests in USD, if applicable
        /// </summary>
        public decimal? EmbeddingCostPerMillionTokens { get; set; }

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
        /// Model type for categorization
        /// </summary>
        /// <remarks>
        /// Indicates the type of operations this model cost applies to (chat, embedding, image, audio, video).
        /// </remarks>
        [Required]
        [MaxLength(50)]
        public string ModelType { get; set; } = "chat";

        /// <summary>
        /// Indicates whether this cost configuration is active
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Effective date for this pricing
        /// </summary>
        public DateTime EffectiveDate { get; set; }

        /// <summary>
        /// Optional expiry date for this pricing
        /// </summary>
        public DateTime? ExpiryDate { get; set; }

        /// <summary>
        /// Optional description for this model cost entry
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Priority value for this model cost entry
        /// </summary>
        /// <remarks>
        /// Higher priority patterns are evaluated first when matching model names.
        /// </remarks>
        public int Priority { get; set; }

        /// <summary>
        /// Cost per minute for audio transcription (speech-to-text) in USD, if applicable
        /// </summary>
        public decimal? AudioCostPerMinute { get; set; }

        /// <summary>
        /// Cost per 1000 characters for text-to-speech synthesis in USD, if applicable
        /// </summary>
        public decimal? AudioCostPerKCharacters { get; set; }

        /// <summary>
        /// Cost per minute for real-time audio input in USD, if applicable
        /// </summary>
        public decimal? AudioInputCostPerMinute { get; set; }

        /// <summary>
        /// Cost per minute for real-time audio output in USD, if applicable
        /// </summary>
        public decimal? AudioOutputCostPerMinute { get; set; }

        /// <summary>
        /// Cost per second for video generation in USD, if applicable
        /// </summary>
        public decimal? VideoCostPerSecond { get; set; }

        /// <summary>
        /// Resolution-based cost multipliers for video generation as JSON string
        /// </summary>
        /// <remarks>
        /// JSON object containing resolution-to-multiplier mappings.
        /// Example: {"720p": 1.0, "1080p": 1.5, "4k": 2.5}
        /// </remarks>
        public string? VideoResolutionMultipliers { get; set; }

        /// <summary>
        /// Cost multiplier for batch processing operations, if applicable
        /// </summary>
        /// <remarks>
        /// This represents a cost reduction factor for batch API usage.
        /// Example: 0.5 means 50% discount (half price), 0.6 means 40% discount.
        /// Applied to the standard token costs when requests are processed through batch APIs.
        /// </remarks>
        public decimal? BatchProcessingMultiplier { get; set; }

        /// <summary>
        /// Indicates if this model supports batch processing
        /// </summary>
        /// <remarks>
        /// When true, requests can be processed through batch endpoints with the BatchProcessingMultiplier discount applied.
        /// </remarks>
        public bool SupportsBatchProcessing { get; set; }

        /// <summary>
        /// Quality-based cost multipliers for image generation as JSON string
        /// </summary>
        /// <remarks>
        /// JSON object containing quality-to-multiplier mappings.
        /// Example: {"standard": 1.0, "hd": 2.0}
        /// </remarks>
        public string? ImageQualityMultipliers { get; set; }

        /// <summary>
        /// Cost per million cached input tokens for prompt caching in USD, if applicable
        /// </summary>
        /// <remarks>
        /// This represents the cost for processing one million cached input tokens (reading from cache).
        /// Used by providers like Anthropic Claude and Google Gemini that offer prompt caching.
        /// Typically much lower than standard input token costs (e.g., 10% of regular cost).
        /// </remarks>
        public decimal? CachedInputCostPerMillionTokens { get; set; }

        /// <summary>
        /// Cost per million tokens for writing to the prompt cache in USD, if applicable
        /// </summary>
        /// <remarks>
        /// This represents the cost for writing one million tokens to the prompt cache.
        /// Used by providers like Anthropic Claude and Google Gemini that offer prompt caching.
        /// The write cost is incurred when new content is added to the cache.
        /// </remarks>
        public decimal? CachedInputWriteCostPerMillionTokens { get; set; }

        /// <summary>
        /// Cost per search unit for reranking models in USD per 1000 units, if applicable
        /// </summary>
        /// <remarks>
        /// Used by reranking models like Cohere Rerank that charge per search unit rather than per token.
        /// A search unit typically consists of 1 query + up to 100 documents to be ranked.
        /// Documents over 500 tokens are split into chunks, each counting as a separate document.
        /// </remarks>
        public decimal? CostPerSearchUnit { get; set; }

        /// <summary>
        /// Cost per inference step for image generation models in USD, if applicable
        /// </summary>
        /// <remarks>
        /// Used by providers like Fireworks that charge based on the number of iterative refinement steps.
        /// Different models require different numbers of steps to generate an image.
        /// Example: FLUX.1[schnell] uses 4 steps × $0.00035/step = $0.0014 per image.
        /// Example: SDXL typically uses 30 steps × $0.00013/step = $0.0039 per image.
        /// </remarks>
        public decimal? CostPerInferenceStep { get; set; }

        /// <summary>
        /// Default number of inference steps for this model
        /// </summary>
        /// <remarks>
        /// Indicates the standard number of iterative refinement steps this model uses for image generation.
        /// Used when the client request doesn't specify a custom step count.
        /// Example: FLUX.1[schnell] uses 4 steps for fast generation, SDXL uses 30 steps for higher quality.
        /// </remarks>
        public int? DefaultInferenceSteps { get; set; }
    }

    /// <summary>
    /// Data transfer object for creating a model cost entry
    /// </summary>
    public class CreateModelCostDto
    {
        /// <summary>
        /// User-friendly name for this cost configuration
        /// </summary>
        /// <remarks>
        /// Examples: "GPT-4 Standard Pricing", "Llama 3 Unified Cost", "Embedding Models - Ada"
        /// </remarks>
        [Required]
        [MaxLength(255)]
        public string CostName { get; set; } = string.Empty;

        /// <summary>
        /// List of model mapping IDs to associate with this cost
        /// </summary>
        /// <remarks>
        /// These are the IDs of ModelProviderMapping entities that should use this cost configuration.
        /// </remarks>
        public List<int> ModelProviderMappingIds { get; set; } = new List<int>();

        /// <summary>
        /// Model type for categorization
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string ModelType { get; set; } = "chat";

        /// <summary>
        /// Priority value for pattern matching
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// Optional description
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Cost per million input tokens for chat/completion requests in USD
        /// </summary>
        [Range(0, double.MaxValue)]
        public decimal InputCostPerMillionTokens { get; set; } = 0;

        /// <summary>
        /// Cost per million output tokens for chat/completion requests in USD
        /// </summary>
        [Range(0, double.MaxValue)]
        public decimal OutputCostPerMillionTokens { get; set; } = 0;

        /// <summary>
        /// Cost per million tokens for embedding requests in USD, if applicable
        /// </summary>
        public decimal? EmbeddingCostPerMillionTokens { get; set; }

        /// <summary>
        /// Cost per image for image generation requests in USD, if applicable
        /// </summary>
        public decimal? ImageCostPerImage { get; set; }

        /// <summary>
        /// Cost per minute for audio transcription (speech-to-text) in USD, if applicable
        /// </summary>
        public decimal? AudioCostPerMinute { get; set; }

        /// <summary>
        /// Cost per 1000 characters for text-to-speech synthesis in USD, if applicable
        /// </summary>
        public decimal? AudioCostPerKCharacters { get; set; }

        /// <summary>
        /// Cost per minute for real-time audio input in USD, if applicable
        /// </summary>
        public decimal? AudioInputCostPerMinute { get; set; }

        /// <summary>
        /// Cost per minute for real-time audio output in USD, if applicable
        /// </summary>
        public decimal? AudioOutputCostPerMinute { get; set; }

        /// <summary>
        /// Cost per second for video generation in USD, if applicable
        /// </summary>
        public decimal? VideoCostPerSecond { get; set; }

        /// <summary>
        /// Resolution-based cost multipliers for video generation as JSON string
        /// </summary>
        /// <remarks>
        /// JSON object containing resolution-to-multiplier mappings.
        /// Example: {"720p": 1.0, "1080p": 1.5, "4k": 2.5}
        /// </remarks>
        public string? VideoResolutionMultipliers { get; set; }

        /// <summary>
        /// Cost multiplier for batch processing operations, if applicable
        /// </summary>
        /// <remarks>
        /// This represents a cost reduction factor for batch API usage.
        /// Example: 0.5 means 50% discount (half price), 0.6 means 40% discount.
        /// Applied to the standard token costs when requests are processed through batch APIs.
        /// </remarks>
        public decimal? BatchProcessingMultiplier { get; set; }

        /// <summary>
        /// Indicates if this model supports batch processing
        /// </summary>
        /// <remarks>
        /// When true, requests can be processed through batch endpoints with the BatchProcessingMultiplier discount applied.
        /// </remarks>
        public bool SupportsBatchProcessing { get; set; }

        /// <summary>
        /// Quality-based cost multipliers for image generation as JSON string
        /// </summary>
        /// <remarks>
        /// JSON object containing quality-to-multiplier mappings.
        /// Example: {"standard": 1.0, "hd": 2.0}
        /// </remarks>
        public string? ImageQualityMultipliers { get; set; }

        /// <summary>
        /// Cost per million cached input tokens for prompt caching in USD, if applicable
        /// </summary>
        /// <remarks>
        /// This represents the cost for processing one million cached input tokens (reading from cache).
        /// Used by providers like Anthropic Claude and Google Gemini that offer prompt caching.
        /// Typically much lower than standard input token costs (e.g., 10% of regular cost).
        /// </remarks>
        public decimal? CachedInputCostPerMillionTokens { get; set; }

        /// <summary>
        /// Cost per million tokens for writing to the prompt cache in USD, if applicable
        /// </summary>
        /// <remarks>
        /// This represents the cost for writing one million tokens to the prompt cache.
        /// Used by providers like Anthropic Claude and Google Gemini that offer prompt caching.
        /// The write cost is incurred when new content is added to the cache.
        /// </remarks>
        public decimal? CachedInputWriteCostPerMillionTokens { get; set; }

        /// <summary>
        /// Cost per search unit for reranking models in USD per 1000 units, if applicable
        /// </summary>
        /// <remarks>
        /// Used by reranking models like Cohere Rerank that charge per search unit rather than per token.
        /// A search unit typically consists of 1 query + up to 100 documents to be ranked.
        /// Documents over 500 tokens are split into chunks, each counting as a separate document.
        /// </remarks>
        public decimal? CostPerSearchUnit { get; set; }

        /// <summary>
        /// Cost per inference step for image generation models in USD, if applicable
        /// </summary>
        /// <remarks>
        /// Used by providers like Fireworks that charge based on the number of iterative refinement steps.
        /// Different models require different numbers of steps to generate an image.
        /// Example: FLUX.1[schnell] uses 4 steps × $0.00035/step = $0.0014 per image.
        /// Example: SDXL typically uses 30 steps × $0.00013/step = $0.0039 per image.
        /// </remarks>
        public decimal? CostPerInferenceStep { get; set; }

        /// <summary>
        /// Default number of inference steps for this model
        /// </summary>
        /// <remarks>
        /// Indicates the standard number of iterative refinement steps this model uses for image generation.
        /// Used when the client request doesn't specify a custom step count.
        /// Example: FLUX.1[schnell] uses 4 steps for fast generation, SDXL uses 30 steps for higher quality.
        /// </remarks>
        public int? DefaultInferenceSteps { get; set; }
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
        /// User-friendly name for this cost configuration
        /// </summary>
        /// <remarks>
        /// Examples: "GPT-4 Standard Pricing", "Llama 3 Unified Cost", "Embedding Models - Ada"
        /// </remarks>
        [Required]
        [MaxLength(255)]
        public string CostName { get; set; } = string.Empty;

        /// <summary>
        /// List of model mapping IDs to associate with this cost
        /// </summary>
        /// <remarks>
        /// These are the IDs of ModelProviderMapping entities that should use this cost configuration.
        /// </remarks>
        public List<int> ModelProviderMappingIds { get; set; } = new List<int>();

        /// <summary>
        /// Model type for categorization
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string ModelType { get; set; } = "chat";

        /// <summary>
        /// Priority value for pattern matching
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// Optional description
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Indicates whether this cost configuration is active
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Cost per million input tokens for chat/completion requests in USD
        /// </summary>
        [Range(0, double.MaxValue)]
        public decimal InputCostPerMillionTokens { get; set; } = 0;

        /// <summary>
        /// Cost per million output tokens for chat/completion requests in USD
        /// </summary>
        [Range(0, double.MaxValue)]
        public decimal OutputCostPerMillionTokens { get; set; } = 0;

        /// <summary>
        /// Cost per million tokens for embedding requests in USD, if applicable
        /// </summary>
        public decimal? EmbeddingCostPerMillionTokens { get; set; }

        /// <summary>
        /// Cost per image for image generation requests in USD, if applicable
        /// </summary>
        public decimal? ImageCostPerImage { get; set; }

        /// <summary>
        /// Cost per minute for audio transcription (speech-to-text) in USD, if applicable
        /// </summary>
        public decimal? AudioCostPerMinute { get; set; }

        /// <summary>
        /// Cost per 1000 characters for text-to-speech synthesis in USD, if applicable
        /// </summary>
        public decimal? AudioCostPerKCharacters { get; set; }

        /// <summary>
        /// Cost per minute for real-time audio input in USD, if applicable
        /// </summary>
        public decimal? AudioInputCostPerMinute { get; set; }

        /// <summary>
        /// Cost per minute for real-time audio output in USD, if applicable
        /// </summary>
        public decimal? AudioOutputCostPerMinute { get; set; }

        /// <summary>
        /// Cost per second for video generation in USD, if applicable
        /// </summary>
        public decimal? VideoCostPerSecond { get; set; }

        /// <summary>
        /// Resolution-based cost multipliers for video generation as JSON string
        /// </summary>
        /// <remarks>
        /// JSON object containing resolution-to-multiplier mappings.
        /// Example: {"720p": 1.0, "1080p": 1.5, "4k": 2.5}
        /// </remarks>
        public string? VideoResolutionMultipliers { get; set; }

        /// <summary>
        /// Cost multiplier for batch processing operations, if applicable
        /// </summary>
        /// <remarks>
        /// This represents a cost reduction factor for batch API usage.
        /// Example: 0.5 means 50% discount (half price), 0.6 means 40% discount.
        /// Applied to the standard token costs when requests are processed through batch APIs.
        /// </remarks>
        public decimal? BatchProcessingMultiplier { get; set; }

        /// <summary>
        /// Indicates if this model supports batch processing
        /// </summary>
        /// <remarks>
        /// When true, requests can be processed through batch endpoints with the BatchProcessingMultiplier discount applied.
        /// </remarks>
        public bool SupportsBatchProcessing { get; set; }

        /// <summary>
        /// Quality-based cost multipliers for image generation as JSON string
        /// </summary>
        /// <remarks>
        /// JSON object containing quality-to-multiplier mappings.
        /// Example: {"standard": 1.0, "hd": 2.0}
        /// </remarks>
        public string? ImageQualityMultipliers { get; set; }

        /// <summary>
        /// Cost per million cached input tokens for prompt caching in USD, if applicable
        /// </summary>
        /// <remarks>
        /// This represents the cost for processing one million cached input tokens (reading from cache).
        /// Used by providers like Anthropic Claude and Google Gemini that offer prompt caching.
        /// Typically much lower than standard input token costs (e.g., 10% of regular cost).
        /// </remarks>
        public decimal? CachedInputCostPerMillionTokens { get; set; }

        /// <summary>
        /// Cost per million tokens for writing to the prompt cache in USD, if applicable
        /// </summary>
        /// <remarks>
        /// This represents the cost for writing one million tokens to the prompt cache.
        /// Used by providers like Anthropic Claude and Google Gemini that offer prompt caching.
        /// The write cost is incurred when new content is added to the cache.
        /// </remarks>
        public decimal? CachedInputWriteCostPerMillionTokens { get; set; }

        /// <summary>
        /// Cost per search unit for reranking models in USD per 1000 units, if applicable
        /// </summary>
        /// <remarks>
        /// Used by reranking models like Cohere Rerank that charge per search unit rather than per token.
        /// A search unit typically consists of 1 query + up to 100 documents to be ranked.
        /// Documents over 500 tokens are split into chunks, each counting as a separate document.
        /// </remarks>
        public decimal? CostPerSearchUnit { get; set; }

        /// <summary>
        /// Cost per inference step for image generation models in USD, if applicable
        /// </summary>
        /// <remarks>
        /// Used by providers like Fireworks that charge based on the number of iterative refinement steps.
        /// Different models require different numbers of steps to generate an image.
        /// Example: FLUX.1[schnell] uses 4 steps × $0.00035/step = $0.0014 per image.
        /// Example: SDXL typically uses 30 steps × $0.00013/step = $0.0039 per image.
        /// </remarks>
        public decimal? CostPerInferenceStep { get; set; }

        /// <summary>
        /// Default number of inference steps for this model
        /// </summary>
        /// <remarks>
        /// Indicates the standard number of iterative refinement steps this model uses for image generation.
        /// Used when the client request doesn't specify a custom step count.
        /// Example: FLUX.1[schnell] uses 4 steps for fast generation, SDXL uses 30 steps for higher quality.
        /// </remarks>
        public int? DefaultInferenceSteps { get; set; }
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
