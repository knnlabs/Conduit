using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces;

/// <summary>
/// Defines the contract for interacting with a Large Language Model (LLM) provider.
/// </summary>
/// <remarks>
/// <para>
/// The ILLMClient interface represents the core abstraction for interacting with LLM provider APIs.
/// It defines a standard set of operations that all LLM providers should implement, regardless
/// of their underlying API differences.
/// </para>
/// <para>
/// Each implementation of this interface handles the provider-specific details of:
/// </para>
/// <list type="bullet">
/// <item><description>Authentication and API key management</description></item>
/// <item><description>Request formatting and serialization</description></item>
/// <item><description>Response parsing and error handling</description></item>
/// <item><description>Rate limiting and retry logic</description></item>
/// <item><description>Provider-specific parameter mappings</description></item>
/// </list>
/// <para>
/// This design allows the rest of the application to work with a consistent interface
/// while the provider-specific implementations handle the details of each API.
/// </para>
/// </remarks>
public interface ILLMClient
{
    /// <summary>
    /// Creates a chat completion based on the provided request.
    /// </summary>
    /// <param name="request">The chat completion request containing messages and generation parameters.</param>
    /// <param name="apiKey">Optional API key override to use instead of the client's configured key.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The chat completion response from the model.</returns>
    /// <exception cref="ValidationException">Thrown when the request fails validation.</exception>
    /// <exception cref="LLMCommunicationException">Thrown when there is an error communicating with the provider.</exception>
    /// <exception cref="ModelUnavailableException">Thrown when the requested model is not available.</exception>
    /// <remarks>
    /// <para>
    /// This method sends a completion request to the LLM provider's API and awaits the full
    /// response. For streaming responses, use <see cref="StreamChatCompletionAsync"/> instead.
    /// </para>
    /// <para>
    /// The request can include different types of messages (system, user, assistant) and may
    /// specify model-specific parameters like temperature, maximum tokens, etc. The client
    /// implementation will map these parameters to the provider's API format.
    /// </para>
    /// <para>
    /// If the request specifies a model, the client will use that model if available. Otherwise,
    /// it will use the model configured for this client instance.
    /// </para>
    /// </remarks>
    Task<ChatCompletionResponse> CreateChatCompletionAsync(
        ChatCompletionRequest request,
        string? apiKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a streaming chat completion based on the provided request.
    /// </summary>
    /// <param name="request">The chat completion request containing messages and generation parameters.</param>
    /// <param name="apiKey">Optional API key override to use instead of the client's configured key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An asynchronous enumerable of chat completion chunks representing the streaming response.</returns>
    /// <exception cref="ValidationException">Thrown when the request fails validation.</exception>
    /// <exception cref="LLMCommunicationException">Thrown when there is an error communicating with the provider.</exception>
    /// <exception cref="ModelUnavailableException">Thrown when the requested model is not available.</exception>
    /// <remarks>
    /// <para>
    /// This method is similar to <see cref="CreateChatCompletionAsync"/> but returns a stream of
    /// partial completion chunks as they become available from the provider, instead of waiting
    /// for the entire response. This enables implementations like typewriter-style UIs where
    /// text appears incrementally.
    /// </para>
    /// <para>
    /// The stream typically consists of:
    /// </para>
    /// <list type="bullet">
    /// <item><description>An initial chunk that may contain role information</description></item>
    /// <item><description>Multiple content chunks with words or tokens</description></item>
    /// <item><description>A final chunk with finish reason</description></item>
    /// </list>
    /// <para>
    /// Not all providers support streaming. Implementations should throw a
    /// <see cref="NotSupportedException"/> if streaming is not supported.
    /// </para>
    /// </remarks>
    IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
        ChatCompletionRequest request,
        string? apiKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the models available from the provider.
    /// </summary>
    /// <param name="apiKey">Optional API key override to use instead of the client's configured key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of available model IDs.</returns>
    /// <exception cref="LLMCommunicationException">Thrown when there is an error communicating with the provider.</exception>
    /// <remarks>
    /// <para>
    /// This method retrieves the list of model IDs that are available from the provider. 
    /// The list typically includes base models and fine-tuned models that the API key has access to.
    /// </para>
    /// <para>
    /// Some providers might not support listing models or might require specific permissions.
    /// In such cases, implementations may return a predefined list of known models or
    /// throw a <see cref="NotSupportedException"/>.
    /// </para>
    /// <para>
    /// Note that availability of a model in this list does not guarantee it can be used
    /// for all operations - some models might be limited to certain features
    /// (e.g., chat-only or embedding-only models).
    /// </para>
    /// </remarks>
    Task<List<string>> ListModelsAsync(
        string? apiKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates embeddings for the provided text.
    /// </summary>
    /// <param name="request">The embedding request containing the text to embed.</param>
    /// <param name="apiKey">Optional API key override to use instead of the client's configured key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The embedding response containing vector representations of the input text.</returns>
    /// <exception cref="ValidationException">Thrown when the request fails validation.</exception>
    /// <exception cref="LLMCommunicationException">Thrown when there is an error communicating with the provider.</exception>
    /// <exception cref="NotSupportedException">Thrown when the provider does not support embeddings.</exception>
    /// <remarks>
    /// <para>
    /// Embeddings are vector representations of text that capture semantic meaning, allowing
    /// for operations like semantic search, clustering, and similarity comparisons. Each text input
    /// is transformed into a high-dimensional vector where semantic similarity is represented
    /// by vector proximity.
    /// </para>
    /// <para>
    /// The request can contain a single string or multiple strings for batch processing.
    /// Each input string will generate a corresponding vector in the response.
    /// </para>
    /// <para>
    /// Not all providers support embeddings. Implementations should throw a
    /// <see cref="NotSupportedException"/> if embeddings are not supported.
    /// </para>
    /// </remarks>
    Task<EmbeddingResponse> CreateEmbeddingAsync(
        EmbeddingRequest request,
        string? apiKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an image based on the provided request.
    /// </summary>
    /// <param name="request">The image generation request containing the prompt and generation parameters.</param>
    /// <param name="apiKey">Optional API key override to use instead of the client's configured key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The image generation response containing URLs or base64-encoded images.</returns>
    /// <exception cref="ValidationException">Thrown when the request fails validation.</exception>
    /// <exception cref="LLMCommunicationException">Thrown when there is an error communicating with the provider.</exception>
    /// <exception cref="NotSupportedException">Thrown when the provider does not support image generation.</exception>
    /// <remarks>
    /// <para>
    /// This method generates images based on a text prompt. The request can specify parameters
    /// such as image size, style, quality, and the number of images to generate.
    /// </para>
    /// <para>
    /// The response typically contains either URLs to the generated images or the images themselves
    /// encoded as base64 strings, depending on the provider and the requested response format.
    /// </para>
    /// <para>
    /// Not all providers support image generation. Implementations should throw a
    /// <see cref="NotSupportedException"/> if image generation is not supported.
    /// </para>
    /// </remarks>
    Task<ImageGenerationResponse> CreateImageAsync(
        ImageGenerationRequest request,
        string? apiKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the capabilities supported by this provider/model combination.
    /// </summary>
    /// <param name="modelId">Optional specific model ID to get capabilities for.</param>
    /// <returns>The provider capabilities including supported parameters and features.</returns>
    /// <exception cref="LLMCommunicationException">Thrown when there is an error communicating with the provider.</exception>
    /// <remarks>
    /// <para>
    /// This method returns information about what parameters and features are supported by
    /// the provider and optionally a specific model. This enables better UX by allowing
    /// interfaces to show/hide parameters based on capabilities.
    /// </para>
    /// <para>
    /// The capabilities include information about:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Supported chat completion parameters (temperature, top_p, etc.)</description></item>
    /// <item><description>Parameter constraints and valid ranges</description></item>
    /// <item><description>Supported features (streaming, embeddings, vision, etc.)</description></item>
    /// </list>
    /// <para>
    /// If a specific model ID is provided, the capabilities returned will be specific to that model.
    /// Otherwise, the capabilities will be for the provider or default model.
    /// </para>
    /// </remarks>
    Task<ConduitLLM.Core.Models.ProviderCapabilities> GetCapabilitiesAsync(
        string? modelId = null);
}
