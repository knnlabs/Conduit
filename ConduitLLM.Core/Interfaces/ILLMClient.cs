using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces;

/// <summary>
/// Defines the contract for interacting with a Large Language Model (LLM) provider.
/// </summary>
public interface ILLMClient
{
    /// <summary>
    /// Creates a chat completion based on the provided request.
    /// </summary>
    /// <param name="request">The chat completion request details.</param>
    /// <param name="apiKey">Optional API key to override the configured key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The chat completion response from the LLM.</returns>
    Task<ChatCompletionResponse> CreateChatCompletionAsync(
        ChatCompletionRequest request,
        string? apiKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a streaming chat completion based on the provided request.
    /// </summary>
    /// <param name="request">The chat completion request details.</param>
    /// <param name="apiKey">Optional API key to override the configured key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An asynchronous enumerable of chat completion chunks.</returns>
    IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
        ChatCompletionRequest request,
        string? apiKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the models available from the provider.
    /// </summary>
    /// <param name="apiKey">Optional API key to override the configured key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of available model IDs.</returns>
    Task<List<string>> ListModelsAsync(
        string? apiKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an embedding based on the provided request.
    /// </summary>
    /// <param name="request">The embedding request details.</param>
    /// <param name="apiKey">Optional API key to override the configured key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The embedding response from the LLM.</returns>
    Task<EmbeddingResponse> CreateEmbeddingAsync(
        EmbeddingRequest request,
        string? apiKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an image based on the provided request.
    /// </summary>
    /// <param name="request">The image generation request details.</param>
    /// <param name="apiKey">Optional API key to override the configured key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The image generation response from the LLM.</returns>
    Task<ImageGenerationResponse> CreateImageAsync(
        ImageGenerationRequest request,
        string? apiKey = null,
        CancellationToken cancellationToken = default);
}
