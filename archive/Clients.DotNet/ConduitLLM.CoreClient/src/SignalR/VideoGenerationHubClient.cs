using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using ConduitLLM.CoreClient.Models;

namespace ConduitLLM.CoreClient.SignalR;

/// <summary>
/// SignalR client for the Video Generation Hub, providing real-time video generation progress notifications.
/// </summary>
public class VideoGenerationHubClient : BaseSignalRConnection, IVideoGenerationHubServer
{
    /// <summary>
    /// Initializes a new instance of the VideoGenerationHubClient class.
    /// </summary>
    /// <param name="baseUrl">Base URL of the Conduit Core API.</param>
    /// <param name="virtualKey">Virtual key for authentication.</param>
    /// <param name="logger">Optional logger instance.</param>
    public VideoGenerationHubClient(string baseUrl, string virtualKey, ILogger<VideoGenerationHubClient>? logger = null)
        : base(baseUrl, virtualKey, logger)
    {
    }

    /// <summary>
    /// Gets the hub path for video generation notifications.
    /// </summary>
    protected override string HubPath => SignalREndpoints.VideoGenerationHub;

    #region Events

    /// <summary>
    /// Event raised when video generation starts.
    /// </summary>
    public event Func<string, object, Task>? VideoGenerationStarted;

    /// <summary>
    /// Event raised when video generation progress is updated.
    /// </summary>
    public event Func<string, int, string?, Task>? VideoGenerationProgress;

    /// <summary>
    /// Event raised when video generation completes successfully.
    /// </summary>
    public event Func<string, AsyncVideoGenerationResponse, Task>? VideoGenerationCompleted;

    /// <summary>
    /// Event raised when video generation fails.
    /// </summary>
    public event Func<string, string, Task>? VideoGenerationFailed;

    #endregion

    /// <summary>
    /// Configures the hub-specific event handlers.
    /// </summary>
    /// <param name="connection">The hub connection to configure.</param>
    protected override void ConfigureHubHandlers(HubConnection connection)
    {
        connection.On<string, object>("VideoGenerationStarted", async (taskId, metadata) =>
        {
            _logger?.LogDebug("Video generation started: {TaskId}", taskId);
            if (VideoGenerationStarted != null)
            {
                await VideoGenerationStarted.Invoke(taskId, metadata);
            }
        });

        connection.On<string, int, string?>("VideoGenerationProgress", async (taskId, progress, message) =>
        {
            _logger?.LogDebug("Video generation progress: {TaskId}, Progress: {Progress}%", taskId, progress);
            if (VideoGenerationProgress != null)
            {
                await VideoGenerationProgress.Invoke(taskId, progress, message);
            }
        });

        connection.On<string, AsyncVideoGenerationResponse>("VideoGenerationCompleted", async (taskId, result) =>
        {
            _logger?.LogDebug("Video generation completed: {TaskId}", taskId);
            if (VideoGenerationCompleted != null)
            {
                await VideoGenerationCompleted.Invoke(taskId, result);
            }
        });

        connection.On<string, string>("VideoGenerationFailed", async (taskId, error) =>
        {
            _logger?.LogDebug("Video generation failed: {TaskId}, Error: {Error}", taskId, error);
            if (VideoGenerationFailed != null)
            {
                await VideoGenerationFailed.Invoke(taskId, error);
            }
        });
    }

    #region IVideoGenerationHubServer Implementation

    /// <summary>
    /// Subscribe to notifications for a specific video generation task.
    /// </summary>
    /// <param name="taskId">Video generation task identifier.</param>
    public async Task SubscribeToTask(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            throw new ArgumentException("Task ID cannot be null or empty", nameof(taskId));

        await InvokeAsync("SubscribeToTask", new object[] { taskId });
        _logger?.LogDebug("Subscribed to video generation task: {TaskId}", taskId);
    }

    /// <summary>
    /// Unsubscribe from notifications for a specific video generation task.
    /// </summary>
    /// <param name="taskId">Video generation task identifier.</param>
    public async Task UnsubscribeFromTask(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            throw new ArgumentException("Task ID cannot be null or empty", nameof(taskId));

        await InvokeAsync("UnsubscribeFromTask", new object[] { taskId });
        _logger?.LogDebug("Unsubscribed from video generation task: {TaskId}", taskId);
    }

    #endregion

    #region Convenience Methods

    /// <summary>
    /// Subscribe to multiple video generation tasks at once.
    /// </summary>
    /// <param name="taskIds">Video generation task identifiers to subscribe to.</param>
    public async Task SubscribeToTasksAsync(IEnumerable<string> taskIds)
    {
        var tasks = taskIds.Select(SubscribeToTask);
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Unsubscribe from multiple video generation tasks at once.
    /// </summary>
    /// <param name="taskIds">Video generation task identifiers to unsubscribe from.</param>
    public async Task UnsubscribeFromTasksAsync(IEnumerable<string> taskIds)
    {
        var tasks = taskIds.Select(UnsubscribeFromTask);
        await Task.WhenAll(tasks);
    }

    #endregion
}