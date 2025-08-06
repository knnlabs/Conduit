using System.Collections.Generic;

namespace ConduitLLM.Core.Interfaces.Configuration
{
    /// <summary>
    /// Service interface for retrieving model cost information.
    /// </summary>
    public interface IModelCostService
    {
        /// <summary>
        /// Gets the cost information for a specific model.
        /// </summary>
        /// <param name="modelId">The model identifier to get costs for.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The model cost information or null if not found.</returns>
        Task<ModelCostInfo?> GetCostForModelAsync(string modelId, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents cost information for a model.
    /// </summary>
    public class ModelCostInfo
    {
        /// <summary>
        /// The model identification pattern.
        /// </summary>
        public string ModelIdPattern { get; set; } = string.Empty;

        /// <summary>
        /// Cost per million input tokens for chat/completion requests.
        /// </summary>
        public decimal InputCostPerMillionTokens { get; set; }

        /// <summary>
        /// Cost per million output tokens for chat/completion requests.
        /// </summary>
        public decimal OutputCostPerMillionTokens { get; set; }

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
        /// Additional cost multipliers for different video resolutions.
        /// Key is resolution (e.g., "1920x1080"), value is multiplier.
        /// </summary>
        public Dictionary<string, decimal>? VideoResolutionMultipliers { get; set; }

        /// <summary>
        /// Cost multiplier for batch processing operations, if applicable.
        /// Example: 0.5 means 50% discount (half price), 0.6 means 40% discount.
        /// </summary>
        public decimal? BatchProcessingMultiplier { get; set; }

        /// <summary>
        /// Indicates if this model supports batch processing.
        /// When true, requests can be processed through batch endpoints with the BatchProcessingMultiplier discount applied.
        /// </summary>
        public bool SupportsBatchProcessing { get; set; }

        /// <summary>
        /// Additional cost multipliers for different image quality levels.
        /// Key is quality level (e.g., "standard", "hd"), value is multiplier.
        /// </summary>
        public Dictionary<string, decimal>? ImageQualityMultipliers { get; set; }

        /// <summary>
        /// Cost per million cached input tokens for prompt caching, if applicable.
        /// Used by providers like Anthropic Claude and Google Gemini that offer prompt caching.
        /// Typically much lower than standard input token costs (e.g., 10% of regular cost).
        /// </summary>
        public decimal? CachedInputCostPerMillionTokens { get; set; }

        /// <summary>
        /// Cost per million tokens for writing to the prompt cache, if applicable.
        /// Used by providers like Anthropic Claude and Google Gemini that offer prompt caching.
        /// The write cost is incurred when new content is added to the cache.
        /// </summary>
        public decimal? CachedInputWriteCostPerMillionTokens { get; set; }

        /// <summary>
        /// Cost per search unit for reranking models, if applicable.
        /// Used by reranking models like Cohere Rerank that charge per search unit rather than per token.
        /// A search unit typically consists of 1 query + up to 100 documents to be ranked.
        /// Cost is expressed as USD per 1000 search units.
        /// </summary>
        public decimal? CostPerSearchUnit { get; set; }

        /// <summary>
        /// Cost per inference step for image generation models, if applicable.
        /// Used by providers like Fireworks that charge based on the number of iterative refinement steps.
        /// Different models require different numbers of steps to generate an image.
        /// Example: FLUX.1[schnell] uses 4 steps × $0.00035/step = $0.0014 per image.
        /// </summary>
        public decimal? CostPerInferenceStep { get; set; }

        /// <summary>
        /// Default number of inference steps for this model.
        /// Used when the client request doesn't specify a custom step count.
        /// Example: FLUX.1[schnell] uses 4 steps for fast generation, SDXL uses 30 steps for higher quality.
        /// </summary>
        public int? DefaultInferenceSteps { get; set; }
    }
}
