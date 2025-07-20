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
        /// Cost per cached input token for prompt caching in USD, if applicable
        /// </summary>
        /// <remarks>
        /// This represents the cost for processing cached input tokens (reading from cache).
        /// Used by providers like Anthropic Claude and Google Gemini that offer prompt caching.
        /// Typically much lower than standard input token costs (e.g., 10% of regular cost).
        /// </remarks>
        public decimal? CachedInputTokenCost { get; set; }

        /// <summary>
        /// Cost per token for writing to the prompt cache in USD, if applicable
        /// </summary>
        /// <remarks>
        /// This represents the cost for writing tokens to the prompt cache.
        /// Used by providers like Anthropic Claude and Google Gemini that offer prompt caching.
        /// The write cost is incurred when new content is added to the cache.
        /// </remarks>
        public decimal? CachedInputWriteCost { get; set; }
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
        /// Cost per cached input token for prompt caching in USD, if applicable
        /// </summary>
        /// <remarks>
        /// This represents the cost for processing cached input tokens (reading from cache).
        /// Used by providers like Anthropic Claude and Google Gemini that offer prompt caching.
        /// Typically much lower than standard input token costs (e.g., 10% of regular cost).
        /// </remarks>
        public decimal? CachedInputTokenCost { get; set; }

        /// <summary>
        /// Cost per token for writing to the prompt cache in USD, if applicable
        /// </summary>
        /// <remarks>
        /// This represents the cost for writing tokens to the prompt cache.
        /// Used by providers like Anthropic Claude and Google Gemini that offer prompt caching.
        /// The write cost is incurred when new content is added to the cache.
        /// </remarks>
        public decimal? CachedInputWriteCost { get; set; }
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
        /// Cost per cached input token for prompt caching in USD, if applicable
        /// </summary>
        /// <remarks>
        /// This represents the cost for processing cached input tokens (reading from cache).
        /// Used by providers like Anthropic Claude and Google Gemini that offer prompt caching.
        /// Typically much lower than standard input token costs (e.g., 10% of regular cost).
        /// </remarks>
        public decimal? CachedInputTokenCost { get; set; }

        /// <summary>
        /// Cost per token for writing to the prompt cache in USD, if applicable
        /// </summary>
        /// <remarks>
        /// This represents the cost for writing tokens to the prompt cache.
        /// Used by providers like Anthropic Claude and Google Gemini that offer prompt caching.
        /// The write cost is incurred when new content is added to the cache.
        /// </remarks>
        public decimal? CachedInputWriteCost { get; set; }
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
