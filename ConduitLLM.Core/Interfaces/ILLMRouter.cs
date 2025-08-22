using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for a router that manages multiple LLM deployments and provides failover, 
    /// load balancing, and optimal model selection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The LLM router is responsible for intelligent routing of LLM requests to the appropriate
    /// model deployment based on various strategies and system conditions. It provides:
    /// </para>
    /// <list type="bullet">
    /// <item>
    ///   <description>Automatic failover when a model is unhealthy or unavailable</description>
    /// </item>
    /// <item>
    ///   <description>Multiple routing strategies (simple, round-robin, least cost, etc.)</description>
    /// </item>
    /// <item>
    ///   <description>Health monitoring of model deployments</description>
    /// </item>
    /// <item>
    ///   <description>Fallback chains for graceful degradation</description>
    /// </item>
    /// <item>
    ///   <description>Load balancing capabilities across equivalent models</description>
    /// </item>
    /// </list>
    /// <para>
    /// The router acts as a higher-level abstraction over the <see cref="ILLMClientFactory"/>,
    /// adding intelligence to the model selection process beyond simple configuration mapping.
    /// </para>
    /// </remarks>
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
        /// <exception cref="ModelUnavailableException">Thrown when no suitable model is available for the request.</exception>
        /// <exception cref="LLMCommunicationException">Thrown when all attempts to communicate with suitable models fail.</exception>
        /// <remarks>
        /// <para>
        /// The router selects the appropriate model based on the specified routing strategy, 
        /// model availability, and health status. It will attempt multiple models if necessary,
        /// based on the configured fallbacks and retry settings.
        /// </para>
        /// <para>
        /// If the request contains a specific model in the <see cref="ChatCompletionRequest.Model"/> property,
        /// the router will attempt to use that model first, then fall back to alternatives if needed.
        /// </para>
        /// <para>
        /// Available routing strategies include:
        /// </para>
        /// <list type="table">
        /// <item>
        ///   <term>simple</term>
        ///   <description>Uses the first available healthy model</description>
        /// </item>
        /// <item>
        ///   <term>roundrobin</term>
        ///   <description>Distributes requests evenly across available models</description>
        /// </item>
        /// <item>
        ///   <term>leastcost</term>
        ///   <description>Selects the model with the lowest token cost</description>
        /// </item>
        /// <item>
        ///   <term>leastlatency</term>
        ///   <description>Selects the model with the lowest average latency</description>
        /// </item>
        /// <item>
        ///   <term>highestpriority</term>
        ///   <description>Selects the model with the highest configured priority</description>
        /// </item>
        /// <item>
        ///   <term>passthrough</term>
        ///   <description>Uses exactly the model specified in the request without routing logic</description>
        /// </item>
        /// </list>
        /// </remarks>
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
        /// <exception cref="ModelUnavailableException">Thrown when no suitable model is available for the request.</exception>
        /// <exception cref="LLMCommunicationException">Thrown when all attempts to communicate with suitable models fail.</exception>
        /// <remarks>
        /// <para>
        /// This method is similar to <see cref="CreateChatCompletionAsync"/> but returns
        /// a stream of completion chunks rather than a complete response. Due to the streaming
        /// nature, the router must select a single model up front rather than retrying
        /// during the stream.
        /// </para>
        /// <para>
        /// The router will mark models as unhealthy if they fail to produce any chunks or
        /// encounter errors during streaming.
        /// </para>
        /// <para>
        /// The same routing strategies available to <see cref="CreateChatCompletionAsync"/> 
        /// are also available for streaming.
        /// </para>
        /// </remarks>
        IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? routingStrategy = null,
            string? apiKey = null,
            CancellationToken cancellationToken = default);


        /// <summary>
        /// Gets the available models for routing.
        /// </summary>
        /// <returns>List of model names available for routing.</returns>
        /// <remarks>
        /// <para>
        /// This method returns all models registered with the router, regardless of
        /// their current health status. Use this to determine which models are
        /// configured in the routing system.
        /// </para>
        /// <para>
        /// For more detailed model information, including capabilities and other metadata,
        /// use <see cref="GetAvailableModelDetailsAsync"/> instead.
        /// </para>
        /// </remarks>
        IReadOnlyList<string> GetAvailableModels();

        /// <summary>
        /// Gets the fallback models for a given model.
        /// </summary>
        /// <param name="modelName">The primary model name.</param>
        /// <returns>List of fallback model names or empty list if none configured.</returns>
        /// <remarks>
        /// <para>
        /// Fallback models are used when the primary model is unavailable or unhealthy.
        /// The router will attempt models in the order they appear in the fallback list.
        /// </para>
        /// <para>
        /// An empty list indicates that no fallbacks are configured for the specified model.
        /// </para>
        /// </remarks>
        IReadOnlyList<string> GetFallbackModels(string modelName);

        /// <summary>
        /// Creates embeddings using the configured routing strategy.
        /// </summary>
        /// <param name="request">The embedding request (model will be determined by router).</param>
        /// <param name="routingStrategy">Optional routing strategy to override the default.</param>
        /// <param name="apiKey">Optional API key to override the configured key.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The embedding response from the selected model.</returns>
        /// <exception cref="ModelUnavailableException">Thrown when no suitable model is available for the request.</exception>
        /// <exception cref="LLMCommunicationException">Thrown when all attempts to communicate with suitable models fail.</exception>
        /// <exception cref="NotSupportedException">Thrown when embedding functionality is not supported by available models.</exception>
        /// <remarks>
        /// <para>
        /// Similar to <see cref="CreateChatCompletionAsync"/>, this method selects an appropriate
        /// model for creating embeddings based on the specified routing strategy and model availability.
        /// </para>
        /// <para>
        /// Note that not all LLM providers support embeddings. The router will automatically
        /// filter to models that support this functionality.
        /// </para>
        /// </remarks>
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
        /// <remarks>
        /// <para>
        /// This method provides more detailed information about available models than
        /// <see cref="GetAvailableModels"/>, including capabilities and other metadata.
        /// </para>
        /// <para>
        /// The information returned is suitable for exposing through a /models API endpoint
        /// that follows the OpenAI API convention.
        /// </para>
        /// </remarks>
        Task<IReadOnlyList<ModelInfo>> GetAvailableModelDetailsAsync(
            CancellationToken cancellationToken = default);
    }
}
