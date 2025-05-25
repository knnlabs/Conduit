using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Main interface for interacting with the ConduitLLM library.
    /// Provides a unified API for accessing different LLM providers.
    /// </summary>
    public interface IConduit
    {
        /// <summary>
        /// Creates a chat completion using the configured LLM providers.
        /// </summary>
        /// <param name="request">The chat completion request, including the target model alias.</param>
        /// <param name="apiKey">Optional API key to override the configured key for this request.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The chat completion response from the selected LLM provider.</returns>
        Task<ChatCompletionResponse> CreateChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a streaming chat completion using the configured LLM providers.
        /// </summary>
        /// <param name="request">The chat completion request, including the target model alias.</param>
        /// <param name="apiKey">Optional API key to override the configured key for this request.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>An asynchronous enumerable of chat completion chunks from the selected LLM provider.</returns>
        IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates an embedding using the configured LLM providers.
        /// </summary>
        /// <param name="request">The embedding request, including the target model alias.</param>
        /// <param name="apiKey">Optional API key to override the configured key for this request.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The embedding response from the selected LLM provider.</returns>
        Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates an image generation using the configured LLM providers.
        /// </summary>
        /// <param name="request">The image generation request, including the target model alias.</param>
        /// <param name="apiKey">Optional API key to override the configured key for this request.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The image generation response from the selected LLM provider.</returns>
        Task<ImageGenerationResponse> CreateImageAsync(
            ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the router instance if one is configured.
        /// </summary>
        /// <returns>The router instance or null if none is configured.</returns>
        ILLMRouter? GetRouter();

        /// <summary>
        /// Gets an LLM client for the specified model.
        /// </summary>
        /// <param name="modelAlias">The model alias to get a client for.</param>
        /// <returns>The LLM client for the specified model.</returns>
        ILLMClient GetClient(string modelAlias);
    }
}