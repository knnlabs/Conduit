using System;
using System.Collections.Generic;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Models.Pricing;

namespace ConduitLLM.Core.Models
{
    /// <summary>
    /// Represents a cached model cost with pre-parsed pricing configuration for performance.
    /// </summary>
    public class CachedModelCost
    {
        /// <summary>
        /// Unique identifier for the model cost entry.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// User-friendly name for this cost configuration.
        /// </summary>
        public string CostName { get; set; } = string.Empty;

        /// <summary>
        /// The pricing model type that determines how costs are calculated.
        /// </summary>
        public PricingModel PricingModel { get; set; } = PricingModel.Standard;

        /// <summary>
        /// Pre-parsed pricing configuration object for performance.
        /// Type depends on PricingModel.
        /// </summary>
        public object? ParsedPricingConfiguration { get; set; }

        /// <summary>
        /// Cost per million input tokens for chat/completion requests.
        /// </summary>
        public decimal InputCostPerMillionTokens { get; set; } = 0;

        /// <summary>
        /// Cost per million output tokens for chat/completion requests.
        /// </summary>
        public decimal OutputCostPerMillionTokens { get; set; } = 0;

        /// <summary>
        /// Cost per million tokens for embedding requests, if applicable.
        /// </summary>
        public decimal? EmbeddingCostPerMillionTokens { get; set; }

        /// <summary>
        /// Cost per image for image generation requests, if applicable.
        /// </summary>
        public decimal? ImageCostPerImage { get; set; }

        /// <summary>
        /// Cost per second for video generation requests, if applicable.
        /// </summary>
        public decimal? VideoCostPerSecond { get; set; }

        /// <summary>
        /// Resolution-based cost multipliers for video generation.
        /// </summary>
        public Dictionary<string, decimal>? VideoResolutionMultipliers { get; set; }

        /// <summary>
        /// Quality-based cost multipliers for image generation.
        /// </summary>
        public Dictionary<string, decimal>? ImageQualityMultipliers { get; set; }

        /// <summary>
        /// Resolution-based cost multipliers for image generation.
        /// </summary>
        public Dictionary<string, decimal>? ImageResolutionMultipliers { get; set; }

        /// <summary>
        /// Cost multiplier for batch processing operations, if applicable.
        /// </summary>
        public decimal? BatchProcessingMultiplier { get; set; }

        /// <summary>
        /// Indicates if this model supports batch processing.
        /// </summary>
        public bool SupportsBatchProcessing { get; set; }

        /// <summary>
        /// Cost per million cached input tokens for prompt caching, if applicable.
        /// </summary>
        public decimal? CachedInputCostPerMillionTokens { get; set; }

        /// <summary>
        /// Cost per million tokens for writing to the prompt cache, if applicable.
        /// </summary>
        public decimal? CachedInputWriteCostPerMillionTokens { get; set; }

        /// <summary>
        /// Cost per search unit for reranking models, if applicable.
        /// </summary>
        public decimal? CostPerSearchUnit { get; set; }

        /// <summary>
        /// Cost per inference step for image generation models, if applicable.
        /// </summary>
        public decimal? CostPerInferenceStep { get; set; }

        /// <summary>
        /// Default number of inference steps for this model.
        /// </summary>
        public int? DefaultInferenceSteps { get; set; }

        /// <summary>
        /// Cost per minute for audio transcription, if applicable.
        /// </summary>
        public decimal? AudioCostPerMinute { get; set; }

        /// <summary>
        /// Cost per 1000 characters for text-to-speech synthesis, if applicable.
        /// </summary>
        public decimal? AudioCostPerKCharacters { get; set; }

        /// <summary>
        /// Cost per minute for real-time audio input, if applicable.
        /// </summary>
        public decimal? AudioInputCostPerMinute { get; set; }

        /// <summary>
        /// Cost per minute for real-time audio output, if applicable.
        /// </summary>
        public decimal? AudioOutputCostPerMinute { get; set; }

        /// <summary>
        /// Model type for categorization.
        /// </summary>
        public string ModelType { get; set; } = "chat";

        /// <summary>
        /// Indicates whether this cost configuration is active.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Priority value for this model cost entry.
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// Optional description for this model cost entry.
        /// </summary>
        public string? Description { get; set; }
    }
}