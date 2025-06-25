using ConduitLLM.Core.Models;
using ConduitLLM.WebUI.Models;
using ConduitLLM.WebUI.Services;

namespace ConduitLLM.WebUI.Interfaces;

/// <summary>
/// Interface for the Conduit API client service
/// </summary>
public interface IConduitApiClient
{
    /// <summary>
    /// Gets the list of available models from the API.
    /// </summary>
    /// <param name="virtualKey">The virtual key to use for authentication.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of model identifiers.</returns>
    Task<List<string>> GetAvailableModelsAsync(string? virtualKey = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a chat completion request to the API.
    /// </summary>
    /// <param name="request">The chat completion request.</param>
    /// <param name="virtualKey">The virtual key to use for authentication.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The chat completion response.</returns>
    Task<ChatCompletionResponse?> CreateChatCompletionAsync(
        ChatCompletionRequest request,
        string? virtualKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a streaming chat completion request to the API.
    /// </summary>
    /// <param name="request">The chat completion request.</param>
    /// <param name="virtualKey">The virtual key to use for authentication.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An async enumerable of streaming chat responses.</returns>
    IAsyncEnumerable<StreamingChatResponse> CreateStreamingChatCompletionAsync(
        ChatCompletionRequest request,
        string? virtualKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an embedding request to the API.
    /// </summary>
    /// <param name="request">The embedding request.</param>
    /// <param name="virtualKey">The virtual key to use for authentication.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The embedding response.</returns>
    Task<EmbeddingResponse?> CreateEmbeddingAsync(
        EmbeddingRequest request,
        string? virtualKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an image generation request to the API.
    /// </summary>
    /// <param name="request">The image generation request.</param>
    /// <param name="virtualKey">The virtual key to use for authentication.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The image generation response.</returns>
    Task<ImageGenerationResponse?> CreateImageAsync(
        ImageGenerationRequest request,
        string? virtualKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an asynchronous image generation task.
    /// </summary>
    /// <param name="request">The image generation request.</param>
    /// <param name="virtualKey">The virtual key to use for authentication.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The task response containing the task ID.</returns>
    Task<ImageGenerationTaskResponse?> CreateImageAsyncTask(
        ImageGenerationRequest request,
        string? virtualKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the status of an image generation task.
    /// </summary>
    /// <param name="taskId">The task ID returned from the async generation endpoint.</param>
    /// <param name="virtualKey">The virtual key to use for authentication.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>Current status of the image generation task.</returns>
    Task<ImageGenerationTaskStatus?> GetImageGenerationStatusAsync(
        string taskId,
        string? virtualKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an image generation task.
    /// </summary>
    /// <param name="taskId">The task ID to cancel.</param>
    /// <param name="virtualKey">The virtual key to use for authentication.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>True if the task was successfully cancelled, false otherwise.</returns>
    Task<bool> CancelImageGenerationAsync(
        string taskId,
        string? virtualKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests if a model supports a specific capability.
    /// </summary>
    /// <param name="modelName">The name of the model to test.</param>
    /// <param name="capability">The capability to test for.</param>
    /// <param name="virtualKey">The virtual key to use for authentication.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>True if the model supports the capability, false otherwise.</returns>
    Task<bool> TestModelCapabilityAsync(
        string modelName,
        string capability,
        string? virtualKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets models available for a specific provider.
    /// </summary>
    /// <param name="providerName">The name of the provider.</param>
    /// <param name="forceRefresh">Whether to force a refresh of cached models.</param>
    /// <param name="virtualKey">The virtual key to use for authentication.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of model identifiers for the provider.</returns>
    Task<List<string>> GetProviderModelsAsync(
        string providerName,
        bool forceRefresh = false,
        string? virtualKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests multiple model capabilities in a single bulk request to reduce API calls.
    /// </summary>
    /// <param name="capabilityTests">List of model-capability pairs to test.</param>
    /// <param name="virtualKey">Optional virtual key to use for authentication.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping model+capability keys to test results.</returns>
    Task<Dictionary<string, bool>> TestBulkModelCapabilitiesAsync(
        List<(string Model, string Capability)> capabilityTests,
        string? virtualKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets discovery information for multiple models in a single bulk request.
    /// </summary>
    /// <param name="modelIds">List of model IDs to get information for.</param>
    /// <param name="virtualKey">Optional virtual key to use for authentication.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping model IDs to their discovery information.</returns>
    Task<Dictionary<string, ModelDiscoveryInfo>> GetBulkModelDiscoveryAsync(
        List<string> modelIds,
        string? virtualKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a video generation request to the API.
    /// </summary>
    /// <param name="request">The video generation request.</param>
    /// <param name="virtualKey">The virtual key to use for authentication.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The video generation response.</returns>
    Task<VideoGenerationResponse?> CreateVideoAsync(
        VideoGenerationRequest request,
        string? virtualKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an asynchronous video generation request to the API.
    /// </summary>
    /// <param name="request">The video generation request.</param>
    /// <param name="virtualKey">The virtual key to use for authentication.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The video generation task response.</returns>
    Task<VideoGenerationTaskResponse?> CreateVideoAsyncTask(
        VideoGenerationRequest request,
        string? virtualKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the status of a video generation task.
    /// </summary>
    /// <param name="taskId">The task ID returned from the async generation endpoint.</param>
    /// <param name="virtualKey">The virtual key to use for authentication.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>Current status of the video generation task.</returns>
    Task<VideoGenerationTaskStatus?> GetVideoGenerationStatusAsync(
        string taskId,
        string? virtualKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a video generation task.
    /// </summary>
    /// <param name="taskId">The task ID to cancel.</param>
    /// <param name="virtualKey">The virtual key to use for authentication.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>True if the task was successfully cancelled, false otherwise.</returns>
    Task<bool> CancelVideoGenerationAsync(
        string taskId,
        string? virtualKey = null,
        CancellationToken cancellationToken = default);
}
