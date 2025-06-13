using ConduitLLM.Core.Models;
using ConduitLLM.WebUI.Models;

namespace ConduitLLM.WebUI.Interfaces;

/// <summary>
/// Interface for the Conduit API client service
/// </summary>
public interface IConduitApiClient
{
    /// <summary>
    /// Gets the list of available models from the API.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of model identifiers.</returns>
    Task<List<string>> GetAvailableModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a chat completion request to the API.
    /// </summary>
    /// <param name="request">The chat completion request.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The chat completion response.</returns>
    Task<ChatCompletionResponse?> CreateChatCompletionAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a streaming chat completion request to the API.
    /// </summary>
    /// <param name="request">The chat completion request.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An async enumerable of streaming chat responses.</returns>
    IAsyncEnumerable<StreamingChatResponse> CreateStreamingChatCompletionAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an embedding request to the API.
    /// </summary>
    /// <param name="request">The embedding request.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The embedding response.</returns>
    Task<EmbeddingResponse?> CreateEmbeddingAsync(
        EmbeddingRequest request,
        CancellationToken cancellationToken = default);
}
