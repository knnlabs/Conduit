using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using ConduitLLM.CoreClient.Models;

namespace ConduitLLM.CoreClient.SignalR;

/// <summary>
/// SignalR client for the Image Generation Hub, providing real-time image generation progress notifications.
/// </summary>
public class ImageGenerationHubClient : BaseSignalRConnection, IImageGenerationHubServer
{
    /// <summary>
    /// Initializes a new instance of the ImageGenerationHubClient class.
    /// </summary>
    /// <param name="baseUrl">Base URL of the Conduit Core API.</param>
    /// <param name="virtualKey">Virtual key for authentication.</param>
    /// <param name="logger">Optional logger instance.</param>
    public ImageGenerationHubClient(string baseUrl, string virtualKey, ILogger<ImageGenerationHubClient>? logger = null)
        : base(baseUrl, virtualKey, logger)
    {
    }

    /// <summary>
    /// Gets the hub path for image generation notifications.
    /// </summary>
    protected override string HubPath => SignalREndpoints.ImageGenerationHub;

    #region Events

    /// <summary>
    /// Event raised when image generation starts.
    /// </summary>
    public event Func<string, object, Task>? ImageGenerationStarted;

    /// <summary>
    /// Event raised when image generation progress is updated.
    /// </summary>
    public event Func<string, int, string?, Task>? ImageGenerationProgress;

    /// <summary>
    /// Event raised when image generation completes successfully.
    /// </summary>
    public event Func<string, AsyncImageGenerationResponse, Task>? ImageGenerationCompleted;

    /// <summary>
    /// Event raised when image generation fails.
    /// </summary>
    public event Func<string, string, Task>? ImageGenerationFailed;

    /// <summary>
    /// Event raised when image generation is cancelled.
    /// </summary>
    public event Func<string, string?, Task>? ImageGenerationCancelled;

    #endregion

    /// <summary>
    /// Configures the hub-specific event handlers.
    /// </summary>
    /// <param name="connection">The hub connection to configure.</param>
    protected override void ConfigureHubHandlers(HubConnection connection)
    {
        connection.On<string, object>("ImageGenerationStarted", async (taskId, metadata) =>
        {
            _logger?.LogDebug("Image generation started: {TaskId}", taskId);
            if (ImageGenerationStarted != null)
            {
                await ImageGenerationStarted.Invoke(taskId, metadata);
            }
        });

        connection.On<string, int, string?>("ImageGenerationProgress", async (taskId, progress, message) =>
        {
            _logger?.LogDebug("Image generation progress: {TaskId}, Progress: {Progress}%", taskId, progress);
            if (ImageGenerationProgress != null)
            {
                await ImageGenerationProgress.Invoke(taskId, progress, message);
            }
        });

        connection.On<string, AsyncImageGenerationResponse>("ImageGenerationCompleted", async (taskId, result) =>
        {
            _logger?.LogDebug("Image generation completed: {TaskId}", taskId);
            if (ImageGenerationCompleted != null)
            {
                await ImageGenerationCompleted.Invoke(taskId, result);
            }
        });

        connection.On<string, string>("ImageGenerationFailed", async (taskId, error) =>
        {
            _logger?.LogDebug("Image generation failed: {TaskId}, Error: {Error}", taskId, error);
            if (ImageGenerationFailed != null)
            {
                await ImageGenerationFailed.Invoke(taskId, error);
            }
        });

        connection.On<string, string?>("ImageGenerationCancelled", async (taskId, reason) =>
        {
            _logger?.LogDebug("Image generation cancelled: {TaskId}, Reason: {Reason}", taskId, reason);
            if (ImageGenerationCancelled != null)
            {
                await ImageGenerationCancelled.Invoke(taskId, reason);
            }
        });
    }

    #region IImageGenerationHubServer Implementation

    /// <summary>
    /// Subscribe to notifications for a specific image generation task.
    /// </summary>
    /// <param name="taskId">Image generation task identifier.</param>
    public async Task SubscribeToTask(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            throw new ArgumentException("Task ID cannot be null or empty", nameof(taskId));

        await InvokeAsync("SubscribeToTask", new object[] { taskId });
        _logger?.LogDebug("Subscribed to image generation task: {TaskId}", taskId);
    }

    /// <summary>
    /// Unsubscribe from notifications for a specific image generation task.
    /// </summary>
    /// <param name="taskId">Image generation task identifier.</param>
    public async Task UnsubscribeFromTask(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            throw new ArgumentException("Task ID cannot be null or empty", nameof(taskId));

        await InvokeAsync("UnsubscribeFromTask", new object[] { taskId });
        _logger?.LogDebug("Unsubscribed from image generation task: {TaskId}", taskId);
    }

    #endregion

    #region Convenience Methods

    /// <summary>
    /// Subscribe to multiple image generation tasks at once.
    /// </summary>
    /// <param name="taskIds">Image generation task identifiers to subscribe to.</param>
    public async Task SubscribeToTasksAsync(IEnumerable<string> taskIds)
    {
        var tasks = taskIds.Select(SubscribeToTask);
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Unsubscribe from multiple image generation tasks at once.
    /// </summary>
    /// <param name="taskIds">Image generation task identifiers to unsubscribe from.</param>
    public async Task UnsubscribeFromTasksAsync(IEnumerable<string> taskIds)
    {
        var tasks = taskIds.Select(UnsubscribeFromTask);
        await Task.WhenAll(tasks);
    }

    #endregion
}