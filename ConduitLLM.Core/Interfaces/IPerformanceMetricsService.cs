using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Service for calculating and managing performance metrics for LLM operations.
    /// </summary>
    public interface IPerformanceMetricsService
    {
        /// <summary>
        /// Calculates performance metrics for a completed chat completion.
        /// </summary>
        /// <param name="response">The chat completion response.</param>
        /// <param name="elapsedTime">The total elapsed time for the request.</param>
        /// <param name="provider">The provider that handled the request.</param>
        /// <param name="model">The model used for the request.</param>
        /// <param name="streaming">Whether this was a streaming request.</param>
        /// <param name="retryAttempts">Number of retry attempts made.</param>
        /// <returns>Calculated performance metrics.</returns>
        PerformanceMetrics CalculateMetrics(
            ChatCompletionResponse response, 
            TimeSpan elapsedTime,
            string provider,
            string model,
            bool streaming = false,
            int retryAttempts = 0);

        /// <summary>
        /// Creates a performance metrics tracker for streaming responses.
        /// </summary>
        /// <param name="provider">The provider handling the request.</param>
        /// <param name="model">The model being used.</param>
        /// <returns>A streaming metrics tracker.</returns>
        IStreamingMetricsTracker CreateStreamingTracker(string provider, string model);
    }

    /// <summary>
    /// Tracks performance metrics for streaming responses.
    /// </summary>
    public interface IStreamingMetricsTracker
    {
        /// <summary>
        /// Records the time when the first token is received.
        /// </summary>
        void RecordFirstToken();

        /// <summary>
        /// Records a token being received.
        /// </summary>
        void RecordToken();

        /// <summary>
        /// Gets the performance metrics for the streaming response.
        /// </summary>
        /// <param name="usage">Token usage information if available.</param>
        /// <returns>Performance metrics.</returns>
        PerformanceMetrics GetMetrics(Usage? usage = null);
    }
}