using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using ConduitLLM.CoreClient.Models;

namespace ConduitLLM.CoreClient.SignalR;

/// <summary>
/// SignalR client for the Task Hub, providing real-time task progress notifications.
/// </summary>
public class TaskHubClient : BaseSignalRConnection, ITaskHubServer
{
    /// <summary>
    /// Initializes a new instance of the TaskHubClient class.
    /// </summary>
    /// <param name="baseUrl">Base URL of the Conduit Core API.</param>
    /// <param name="virtualKey">Virtual key for authentication.</param>
    /// <param name="logger">Optional logger instance.</param>
    public TaskHubClient(string baseUrl, string virtualKey, ILogger<TaskHubClient>? logger = null)
        : base(baseUrl, virtualKey, logger)
    {
    }

    /// <summary>
    /// Gets the hub path for task notifications.
    /// </summary>
    protected override string HubPath => SignalREndpoints.TaskHub;

    #region Events

    /// <summary>
    /// Event raised when a task is started.
    /// </summary>
    public event Func<string, string, object, Task>? TaskStarted;

    /// <summary>
    /// Event raised when task progress is updated.
    /// </summary>
    public event Func<string, int, string?, Task>? TaskProgress;

    /// <summary>
    /// Event raised when a task completes successfully.
    /// </summary>
    public event Func<string, object, Task>? TaskCompleted;

    /// <summary>
    /// Event raised when a task fails.
    /// </summary>
    public event Func<string, string, bool, Task>? TaskFailed;

    /// <summary>
    /// Event raised when a task is cancelled.
    /// </summary>
    public event Func<string, string?, Task>? TaskCancelled;

    /// <summary>
    /// Event raised when a task times out.
    /// </summary>
    public event Func<string, int, Task>? TaskTimedOut;

    #endregion

    /// <summary>
    /// Configures the hub-specific event handlers.
    /// </summary>
    /// <param name="connection">The hub connection to configure.</param>
    protected override void ConfigureHubHandlers(HubConnection connection)
    {
        connection.On<string, string, object>("TaskStarted", async (taskId, taskType, metadata) =>
        {
            _logger?.LogDebug("Task started: {TaskId}, Type: {TaskType}", taskId, taskType);
            if (TaskStarted != null)
            {
                await TaskStarted.Invoke(taskId, taskType, metadata);
            }
        });

        connection.On<string, int, string?>("TaskProgress", async (taskId, progress, message) =>
        {
            _logger?.LogDebug("Task progress: {TaskId}, Progress: {Progress}%", taskId, progress);
            if (TaskProgress != null)
            {
                await TaskProgress.Invoke(taskId, progress, message);
            }
        });

        connection.On<string, object>("TaskCompleted", async (taskId, result) =>
        {
            _logger?.LogDebug("Task completed: {TaskId}", taskId);
            if (TaskCompleted != null)
            {
                await TaskCompleted.Invoke(taskId, result);
            }
        });

        connection.On<string, string, bool>("TaskFailed", async (taskId, error, isRetryable) =>
        {
            _logger?.LogDebug("Task failed: {TaskId}, Error: {Error}, Retryable: {Retryable}", 
                taskId, error, isRetryable);
            if (TaskFailed != null)
            {
                await TaskFailed.Invoke(taskId, error, isRetryable);
            }
        });

        connection.On<string, string?>("TaskCancelled", async (taskId, reason) =>
        {
            _logger?.LogDebug("Task cancelled: {TaskId}, Reason: {Reason}", taskId, reason);
            if (TaskCancelled != null)
            {
                await TaskCancelled.Invoke(taskId, reason);
            }
        });

        connection.On<string, int>("TaskTimedOut", async (taskId, timeoutSeconds) =>
        {
            _logger?.LogDebug("Task timed out: {TaskId}, Timeout: {Timeout}s", taskId, timeoutSeconds);
            if (TaskTimedOut != null)
            {
                await TaskTimedOut.Invoke(taskId, timeoutSeconds);
            }
        });
    }

    #region ITaskHubServer Implementation

    /// <summary>
    /// Subscribe to notifications for a specific task.
    /// </summary>
    /// <param name="taskId">Task identifier to subscribe to.</param>
    public async Task SubscribeToTask(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            throw new ArgumentException("Task ID cannot be null or empty", nameof(taskId));

        await InvokeAsync("SubscribeToTask", new object[] { taskId });
        _logger?.LogDebug("Subscribed to task: {TaskId}", taskId);
    }

    /// <summary>
    /// Unsubscribe from notifications for a specific task.
    /// </summary>
    /// <param name="taskId">Task identifier to unsubscribe from.</param>
    public async Task UnsubscribeFromTask(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            throw new ArgumentException("Task ID cannot be null or empty", nameof(taskId));

        await InvokeAsync("UnsubscribeFromTask", new object[] { taskId });
        _logger?.LogDebug("Unsubscribed from task: {TaskId}", taskId);
    }

    /// <summary>
    /// Subscribe to notifications for all tasks of a specific type.
    /// </summary>
    /// <param name="taskType">Task type to subscribe to.</param>
    public async Task SubscribeToTaskType(string taskType)
    {
        if (string.IsNullOrWhiteSpace(taskType))
            throw new ArgumentException("Task type cannot be null or empty", nameof(taskType));

        await InvokeAsync("SubscribeToTaskType", new object[] { taskType });
        _logger?.LogDebug("Subscribed to task type: {TaskType}", taskType);
    }

    /// <summary>
    /// Unsubscribe from notifications for a task type.
    /// </summary>
    /// <param name="taskType">Task type to unsubscribe from.</param>
    public async Task UnsubscribeFromTaskType(string taskType)
    {
        if (string.IsNullOrWhiteSpace(taskType))
            throw new ArgumentException("Task type cannot be null or empty", nameof(taskType));

        await InvokeAsync("UnsubscribeFromTaskType", new object[] { taskType });
        _logger?.LogDebug("Unsubscribed from task type: {TaskType}", taskType);
    }

    #endregion

    #region Convenience Methods

    /// <summary>
    /// Subscribe to multiple tasks at once.
    /// </summary>
    /// <param name="taskIds">Task identifiers to subscribe to.</param>
    public async Task SubscribeToTasksAsync(IEnumerable<string> taskIds)
    {
        var tasks = taskIds.Select(SubscribeToTask);
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Unsubscribe from multiple tasks at once.
    /// </summary>
    /// <param name="taskIds">Task identifiers to unsubscribe from.</param>
    public async Task UnsubscribeFromTasksAsync(IEnumerable<string> taskIds)
    {
        var tasks = taskIds.Select(UnsubscribeFromTask);
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Subscribe to multiple task types at once.
    /// </summary>
    /// <param name="taskTypes">Task types to subscribe to.</param>
    public async Task SubscribeToTaskTypesAsync(IEnumerable<string> taskTypes)
    {
        var tasks = taskTypes.Select(SubscribeToTaskType);
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Unsubscribe from multiple task types at once.
    /// </summary>
    /// <param name="taskTypes">Task types to unsubscribe from.</param>
    public async Task UnsubscribeFromTaskTypesAsync(IEnumerable<string> taskTypes)
    {
        var tasks = taskTypes.Select(UnsubscribeFromTaskType);
        await Task.WhenAll(tasks);
    }

    #endregion
}