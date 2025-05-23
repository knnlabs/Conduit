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
        /// Cost per input token for chat/completion requests.
        /// </summary>
        public decimal InputTokenCost { get; set; }

        /// <summary>
        /// Cost per output token for chat/completion requests.
        /// </summary>
        public decimal OutputTokenCost { get; set; }

        /// <summary>
        /// Cost per token for embedding requests, if applicable.
        /// </summary>
        public decimal? EmbeddingTokenCost { get; set; }

        /// <summary>
        /// Cost per image for image generation requests, if applicable.
        /// </summary>
        public decimal? ImageCostPerImage { get; set; }
    }
}