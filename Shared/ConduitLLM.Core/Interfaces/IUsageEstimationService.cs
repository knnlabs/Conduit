using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Service for estimating usage when providers don't return usage data in streaming responses.
    /// This prevents revenue loss by calculating token counts from the actual content.
    /// </summary>
    public interface IUsageEstimationService
    {
        /// <summary>
        /// Estimates usage from a streaming response when the provider doesn't return usage data.
        /// Uses conservative estimation with a buffer to avoid undercharging.
        /// </summary>
        /// <param name="modelId">The model identifier used for the request</param>
        /// <param name="inputMessages">The input messages sent to the model</param>
        /// <param name="streamedContent">The accumulated content from the streaming response</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Estimated usage with prompt and completion tokens</returns>
        Task<Usage> EstimateUsageFromStreamingResponseAsync(
            string modelId,
            List<Message> inputMessages,
            string streamedContent,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Estimates usage from raw input and output text.
        /// Useful for simpler scenarios or testing.
        /// </summary>
        /// <param name="modelId">The model identifier</param>
        /// <param name="inputText">The input text</param>
        /// <param name="outputText">The output text</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Estimated usage with prompt and completion tokens</returns>
        Task<Usage> EstimateUsageFromTextAsync(
            string modelId,
            string inputText,
            string outputText,
            CancellationToken cancellationToken = default);
    }
}