using System.Collections.Generic;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Models; // Ensure this is present

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for a router that manages multiple LLM deployments and provides failover, 
    /// load balancing, and optimal model selection.
    /// </summary>
    public interface ILLMRouter
    {
        /// <summary>
        /// Creates a chat completion using the configured routing strategy.
        /// </summary>
        /// <param name="request">The chat completion request (model will be determined by router).</param>
        /// <param name="routingStrategy">Optional routing strategy to override the default.</param>
        /// <param name="apiKey">Optional API key to override the configured key.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The chat completion response from the selected model.</returns>
        Task<ChatCompletionResponse> CreateChatCompletionAsync(
            ChatCompletionRequest request,
            string? routingStrategy = null,
            string? apiKey = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a streaming chat completion using the configured routing strategy.
        /// </summary>
        /// <param name="request">The chat completion request (model will be determined by router).</param>
        /// <param name="routingStrategy">Optional routing strategy to override the default.</param>
        /// <param name="apiKey">Optional API key to override the configured key.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>An asynchronous enumerable of chat completion chunks.</returns>
        IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? routingStrategy = null,
            string? apiKey = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the health status of a deployment.
        /// </summary>
        /// <param name="modelName">Name of the model to update.</param>
        /// <param name="isHealthy">Whether the model is healthy.</param>
        void UpdateModelHealth(string modelName, bool isHealthy);

        /// <summary>
        /// Gets the available models for routing.
        /// </summary>
        /// <returns>List of model names available for routing.</returns>
        IReadOnlyList<string> GetAvailableModels();

        /// <summary>
        /// Gets the fallback models for a given model.
        /// </summary>
        /// <param name="modelName">The primary model name.</param>
        /// <returns>List of fallback model names or empty list if none configured.</returns>
        IReadOnlyList<string> GetFallbackModels(string modelName);

        /// <summary>
        /// Creates embeddings using the configured routing strategy.
        /// </summary>
        /// <param name="request">The embedding request (model will be determined by router).</param>
        /// <param name="routingStrategy">Optional routing strategy to override the default.</param>
        /// <param name="apiKey">Optional API key to override the configured key.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The embedding response from the selected model.</returns>
        Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
            string? routingStrategy = null,
            string? apiKey = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets detailed information about the available models suitable for the /models endpoint.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A list of detailed model information objects.</returns>
        Task<IReadOnlyList<ModelInfo>> GetAvailableModelDetailsAsync(
            CancellationToken cancellationToken = default);
    }
}
