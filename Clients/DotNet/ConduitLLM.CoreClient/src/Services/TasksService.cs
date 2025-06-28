using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ConduitLLM.CoreClient.Client;
using ConduitLLM.CoreClient.Constants;
using ConduitLLM.CoreClient.Exceptions;
using ConduitLLM.CoreClient.Models;
using ConduitLLM.CoreClient.Utils;

namespace ConduitLLM.CoreClient.Services
{
    /// <summary>
    /// Service for general task management operations using the Conduit Core API.
    /// </summary>
    public class TasksService
    {
        private readonly BaseClient _client;
        private readonly ILogger<TasksService>? _logger;
        private const string TasksEndpoint = ApiEndpoints.V1.Tasks.Base;

        /// <summary>
        /// Initializes a new instance of the <see cref="TasksService"/> class.
        /// </summary>
        /// <param name="client">The base client for making API requests.</param>
        /// <param name="logger">Optional logger for debugging and monitoring.</param>
        public TasksService(BaseClient client, ILogger<TasksService>? logger = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _logger = logger;
        }

        /// <summary>
        /// Gets the status of any task by its ID.
        /// </summary>
        /// <param name="taskId">The task identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The current task status and result if completed.</returns>
        /// <exception cref="ValidationException">Thrown when the task ID is invalid.</exception>
        /// <exception cref="ConduitCoreException">Thrown when the API request fails.</exception>
        public async Task<TaskStatusResponse> GetTaskStatusAsync(
            string taskId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(taskId))
                    throw new ValidationException("Task ID is required", "taskId");

                var endpoint = $"{TasksEndpoint}/{Uri.EscapeDataString(taskId)}";
                
                var response = await _client.GetForServiceAsync<TaskStatusResponse>(
                    endpoint,
                    cancellationToken);

                _logger?.LogDebug("Retrieved status for task {TaskId}: {Status}", 
                    taskId, response.Status);
                
                return response;
            }
            catch (Exception ex) when (!(ex is ConduitCoreException))
            {
                ErrorHandler.HandleException(ex);
                throw;
            }
        }

        /// <summary>
        /// Cancels a pending or running task.
        /// </summary>
        /// <param name="taskId">The task identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="ValidationException">Thrown when the task ID is invalid.</exception>
        /// <exception cref="ConduitCoreException">Thrown when the API request fails.</exception>
        public async Task CancelTaskAsync(
            string taskId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(taskId))
                    throw new ValidationException("Task ID is required", "taskId");

                var endpoint = $"{TasksEndpoint}/{Uri.EscapeDataString(taskId)}/cancel";
                
                await _client.PostForServiceAsync<object>(endpoint, new { }, cancellationToken);
                
                _logger?.LogDebug("Cancelled task {TaskId}", taskId);
            }
            catch (Exception ex) when (!(ex is ConduitCoreException))
            {
                ErrorHandler.HandleException(ex);
                throw;
            }
        }

        /// <summary>
        /// Polls a task until completion or timeout.
        /// </summary>
        /// <param name="taskId">The task identifier.</param>
        /// <param name="options">Polling options.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The final task result when completed.</returns>
        /// <exception cref="ValidationException">Thrown when parameters are invalid.</exception>
        /// <exception cref="TimeoutException">Thrown when polling times out.</exception>
        /// <exception cref="ConduitCoreException">Thrown when the API request fails or task fails.</exception>
        public async Task<T> PollTaskUntilCompletionAsync<T>(
            string taskId,
            TaskPollingOptions? options = null,
            CancellationToken cancellationToken = default) where T : class
        {
            options ??= new TaskPollingOptions();
            
            if (string.IsNullOrWhiteSpace(taskId))
                throw new ValidationException("Task ID is required", "taskId");

            var startTime = DateTime.UtcNow;
            var currentInterval = options.IntervalMs;
            
            _logger?.LogDebug("Starting to poll task {TaskId} with interval {IntervalMs}ms, timeout {TimeoutMs}ms", 
                taskId, options.IntervalMs, options.TimeoutMs);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Check timeout
                if ((DateTime.UtcNow - startTime).TotalMilliseconds > options.TimeoutMs)
                {
                    throw new TimeoutException($"Task polling timed out after {options.TimeoutMs}ms");
                }

                var status = await GetTaskStatusAsync(taskId, cancellationToken);

                switch (status.Status?.ToLowerInvariant())
                {
                    case "completed":
                        if (status.Result == null)
                            throw new ConduitCoreException("Task completed but no result was provided", null, null, null, null);
                        
                        _logger?.LogDebug("Task {TaskId} completed successfully", taskId);
                        return status.Result as T ?? throw new ConduitCoreException("Task result is not of expected type", null, null, null, null);

                    case "failed":
                        throw new ConduitCoreException($"Task failed: {status.Error ?? "Unknown error"}", null, null, null, null);

                    case "cancelled":
                        throw new ConduitCoreException("Task was cancelled", null, null, null, null);

                    case "timedout":
                        throw new ConduitCoreException("Task timed out", null, null, null, null);

                    case "pending":
                    case "running":
                        // Continue polling
                        break;

                    default:
                        throw new ConduitCoreException($"Unknown task status: {status.Status}", null, null, null, null);
                }

                // Wait before next poll
                await Task.Delay(currentInterval, cancellationToken);

                // Apply exponential backoff if enabled
                if (options.UseExponentialBackoff)
                {
                    currentInterval = Math.Min(currentInterval * 2, options.MaxIntervalMs);
                }
            }
        }

        /// <summary>
        /// Requests cleanup of old completed tasks (admin operation).
        /// </summary>
        /// <param name="olderThanHours">Remove tasks older than this number of hours.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of tasks cleaned up.</returns>
        /// <exception cref="ConduitCoreException">Thrown when the API request fails.</exception>
        public async Task<int> CleanupOldTasksAsync(
            int olderThanHours = 24,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var endpoint = $"{TasksEndpoint}/cleanup";
                var request = new { older_than_hours = olderThanHours };
                
                var response = await _client.PostForServiceAsync<CleanupTasksResponse>(
                    endpoint, 
                    request, 
                    cancellationToken);
                
                _logger?.LogDebug("Cleaned up {Count} old tasks", response.TasksRemoved);
                return response.TasksRemoved;
            }
            catch (Exception ex) when (!(ex is ConduitCoreException))
            {
                ErrorHandler.HandleException(ex);
                throw;
            }
        }
    }

    /// <summary>
    /// Represents the response from a general task status request.
    /// </summary>
    public class TaskStatusResponse
    {
        /// <summary>
        /// Gets or sets the unique task identifier.
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("task_id")]
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current status of the task.
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("status")]
        public string? Status { get; set; }

        /// <summary>
        /// Gets or sets the task type.
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("task_type")]
        public string? TaskType { get; set; }

        /// <summary>
        /// Gets or sets the progress percentage (0-100).
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("progress")]
        public int Progress { get; set; }

        /// <summary>
        /// Gets or sets an optional progress message.
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("message")]
        public string? Message { get; set; }

        /// <summary>
        /// Gets or sets when the task was created.
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets when the task was last updated.
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Gets or sets the task result, available when status is completed.
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("result")]
        public object? Result { get; set; }

        /// <summary>
        /// Gets or sets error information if the task failed.
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    /// <summary>
    /// Represents options for polling task status.
    /// </summary>
    public class TaskPollingOptions
    {
        /// <summary>
        /// Gets or sets the polling interval in milliseconds.
        /// </summary>
        public int IntervalMs { get; set; } = 2000;

        /// <summary>
        /// Gets or sets the maximum polling timeout in milliseconds.
        /// </summary>
        public int TimeoutMs { get; set; } = 600000; // 10 minutes

        /// <summary>
        /// Gets or sets whether to use exponential backoff for polling intervals.
        /// </summary>
        public bool UseExponentialBackoff { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum interval between polls in milliseconds when using exponential backoff.
        /// </summary>
        public int MaxIntervalMs { get; set; } = 30000; // 30 seconds
    }

    /// <summary>
    /// Represents the response from a cleanup tasks request.
    /// </summary>
    public class CleanupTasksResponse
    {
        /// <summary>
        /// Gets or sets the number of tasks that were removed.
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("tasks_removed")]
        public int TasksRemoved { get; set; }
    }
}