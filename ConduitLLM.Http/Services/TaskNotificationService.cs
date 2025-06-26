using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Http.Hubs;
using Polly;
using Polly.CircuitBreaker;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Implementation of task notification service that sends real-time updates through SignalR.
    /// Includes retry logic and circuit breaker for resilient hub communication.
    /// </summary>
    public class TaskNotificationService : ITaskNotificationService
    {
        private readonly IHubContext<TaskHub> _taskHubContext;
        private readonly IHubContext<ImageGenerationHub>? _imageHubContext;
        private readonly IHubContext<VideoGenerationHub>? _videoHubContext;
        private readonly ILogger<TaskNotificationService> _logger;
        private readonly IAsyncPolicy _retryPolicy;
        private readonly IAsyncPolicy _circuitBreakerPolicy;
        private readonly IAsyncPolicy _combinedPolicy;
        private readonly SemaphoreSlim _semaphore;

        public TaskNotificationService(
            IHubContext<TaskHub> taskHubContext,
            IHubContext<ImageGenerationHub>? imageHubContext,
            IHubContext<VideoGenerationHub>? videoHubContext,
            ILogger<TaskNotificationService> logger)
        {
            _taskHubContext = taskHubContext ?? throw new ArgumentNullException(nameof(taskHubContext));
            _imageHubContext = imageHubContext;
            _videoHubContext = videoHubContext;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize retry policy - 3 retries with exponential backoff
            _retryPolicy = Policy
                .Handle<Exception>(ex => !(ex is ArgumentException || ex is InvalidOperationException))
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning(exception, 
                            "Retry {RetryCount} after {TimeSpan}s for task notification", 
                            retryCount, timeSpan.TotalSeconds);
                    });

            // Initialize circuit breaker - break after 5 failures for 30 seconds
            _circuitBreakerPolicy = Policy
                .Handle<Exception>(ex => !(ex is ArgumentException || ex is InvalidOperationException))
                .CircuitBreakerAsync(
                    5,
                    TimeSpan.FromSeconds(30),
                    (exception, duration) =>
                    {
                        _logger.LogError(exception, 
                            "Circuit breaker opened for {Duration}s due to repeated failures", 
                            duration.TotalSeconds);
                    },
                    () =>
                    {
                        _logger.LogInformation("Circuit breaker reset, resuming normal operations");
                    });

            // Combine retry and circuit breaker policies
            _combinedPolicy = Policy.WrapAsync(_retryPolicy, _circuitBreakerPolicy);

            // Initialize semaphore for thread-safe operations (allow multiple concurrent notifications)
            _semaphore = new SemaphoreSlim(10, 10);
        }

        public async Task NotifyTaskStartedAsync(string taskId, string taskType, int virtualKeyId, object? metadata = null)
        {
            await ExecuteWithPolicyAsync(async () =>
            {
                // Prepare metadata with virtual key ID
                var enrichedMetadata = EnrichMetadata(metadata, virtualKeyId);

                // Send to unified TaskHub
                await _taskHubContext.Clients.Group($"task-{taskId}")
                    .SendAsync("TaskStarted", taskId, taskType, enrichedMetadata);
                
                await _taskHubContext.Clients.Group($"vkey-{virtualKeyId}-{taskType}")
                    .SendAsync("TaskStarted", taskId, taskType, enrichedMetadata);

                // Send to legacy hubs for backward compatibility
                await SendToLegacyHub(taskType, "TaskStarted", taskId, enrichedMetadata);

                _logger.LogDebug("Notified task started: {TaskId} of type {TaskType} for Virtual Key {VirtualKeyId}",
                    taskId, taskType, virtualKeyId);
            }, $"NotifyTaskStarted:{taskId}");
        }

        public async Task NotifyTaskProgressAsync(string taskId, int progress, string? message = null)
        {
            await ExecuteWithPolicyAsync(async () =>
            {
                // Send to unified TaskHub
                await _taskHubContext.Clients.Group($"task-{taskId}")
                    .SendAsync("TaskProgress", taskId, progress, message);

                // Send to legacy hubs for backward compatibility
                await SendToLegacyHubForTask(taskId, "TaskProgress", taskId, progress, message ?? string.Empty);

                _logger.LogDebug("Notified task progress: {TaskId} at {Progress}% - {Message}",
                    taskId, progress, message);
            }, $"NotifyTaskProgress:{taskId}");
        }

        public async Task NotifyTaskCompletedAsync(string taskId, object? result = null)
        {
            await ExecuteWithPolicyAsync(async () =>
            {
                // Send to unified TaskHub
                await _taskHubContext.Clients.Group($"task-{taskId}")
                    .SendAsync("TaskCompleted", taskId, result);

                // Send to legacy hubs for backward compatibility
                await SendToLegacyHubForTask(taskId, "TaskCompleted", taskId, result ?? new object());

                _logger.LogDebug("Notified task completed: {TaskId}", taskId);
            }, $"NotifyTaskCompleted:{taskId}");
        }

        public async Task NotifyTaskFailedAsync(string taskId, string error, bool isRetryable = false)
        {
            await ExecuteWithPolicyAsync(async () =>
            {
                // Send to unified TaskHub
                await _taskHubContext.Clients.Group($"task-{taskId}")
                    .SendAsync("TaskFailed", taskId, error, isRetryable);

                // Send to legacy hubs for backward compatibility
                await SendToLegacyHubForTask(taskId, "TaskFailed", taskId, error, isRetryable);

                _logger.LogDebug("Notified task failed: {TaskId} with error: {Error} (Retryable: {IsRetryable})",
                    taskId, error, isRetryable);
            }, $"NotifyTaskFailed:{taskId}");
        }

        public async Task NotifyTaskCancelledAsync(string taskId, string? reason = null)
        {
            await ExecuteWithPolicyAsync(async () =>
            {
                // Send to unified TaskHub
                await _taskHubContext.Clients.Group($"task-{taskId}")
                    .SendAsync("TaskCancelled", taskId, reason);

                // Send to legacy hubs for backward compatibility
                await SendToLegacyHubForTask(taskId, "TaskCancelled", taskId, reason ?? string.Empty);

                _logger.LogDebug("Notified task cancelled: {TaskId} with reason: {Reason}",
                    taskId, reason);
            }, $"NotifyTaskCancelled:{taskId}");
        }

        public async Task NotifyTaskTimedOutAsync(string taskId, int timeoutSeconds)
        {
            await ExecuteWithPolicyAsync(async () =>
            {
                // Send to unified TaskHub
                await _taskHubContext.Clients.Group($"task-{taskId}")
                    .SendAsync("TaskTimedOut", taskId, timeoutSeconds);

                // Send to legacy hubs for backward compatibility
                await SendToLegacyHubForTask(taskId, "TaskTimedOut", taskId, timeoutSeconds);

                _logger.LogDebug("Notified task timed out: {TaskId} after {TimeoutSeconds} seconds",
                    taskId, timeoutSeconds);
            }, $"NotifyTaskTimedOut:{taskId}");
        }

        private Dictionary<string, object> EnrichMetadata(object? metadata, int virtualKeyId)
        {
            var enriched = new Dictionary<string, object>
            {
                ["virtualKeyId"] = virtualKeyId
            };

            if (metadata != null)
            {
                if (metadata is IDictionary<string, object> dict)
                {
                    foreach (var kvp in dict)
                    {
                        enriched[kvp.Key] = kvp.Value;
                    }
                }
                else
                {
                    enriched["data"] = metadata;
                }
            }

            return enriched;
        }

        private async Task SendToLegacyHub(string taskType, string method, params object[] args)
        {
            try
            {
                // Route to appropriate legacy hub based on task type
                switch (taskType?.ToLowerInvariant())
                {
                    case "image_generation" when _imageHubContext != null:
                        await _imageHubContext.Clients.All.SendAsync(method, args);
                        break;
                    
                    case "video_generation" when _videoHubContext != null:
                        await _videoHubContext.Clients.All.SendAsync(method, args);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send to legacy hub for task type {TaskType}", taskType);
                // Don't rethrow - legacy hub failures shouldn't break the main notification
            }
        }

        private async Task SendToLegacyHubForTask(string taskId, string method, params object[] args)
        {
            try
            {
                // For progress/completion updates, send to task-specific groups in legacy hubs
                if (_imageHubContext != null)
                {
                    await _imageHubContext.Clients.Group($"image-{taskId}").SendAsync(method, args);
                }
                
                if (_videoHubContext != null)
                {
                    await _videoHubContext.Clients.Group($"video-{taskId}").SendAsync(method, args);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send to legacy hub for task {TaskId}", taskId);
                // Don't rethrow - legacy hub failures shouldn't break the main notification
            }
        }

        /// <summary>
        /// Executes an action with retry and circuit breaker policies for resilient communication.
        /// </summary>
        private async Task ExecuteWithPolicyAsync(Func<Task> action, string operationKey)
        {
            // Ensure thread-safe execution with a reasonable level of concurrency
            await _semaphore.WaitAsync();
            try
            {
                await _combinedPolicy.ExecuteAsync(async (context) =>
                {
                    context["OperationKey"] = operationKey;
                    await action();
                }, new Dictionary<string, object>());
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogError(ex, "Circuit breaker is open for operation {OperationKey}, notification skipped", operationKey);
                // Don't rethrow circuit breaker exceptions to prevent cascading failures
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification for operation {OperationKey} after all retry attempts", operationKey);
                // Don't rethrow to prevent cascading failures in notification scenarios
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Gets the current circuit breaker state for monitoring purposes.
        /// </summary>
        public CircuitState GetCircuitState()
        {
            if (_circuitBreakerPolicy is CircuitBreakerPolicy circuitBreaker)
            {
                return circuitBreaker.CircuitState;
            }
            return CircuitState.Closed;
        }

        /// <summary>
        /// Manually reset the circuit breaker if needed.
        /// </summary>
        public void ResetCircuitBreaker()
        {
            if (_circuitBreakerPolicy is CircuitBreakerPolicy circuitBreaker)
            {
                circuitBreaker.Reset();
                _logger.LogInformation("Circuit breaker manually reset");
            }
        }

        /// <summary>
        /// Get circuit breaker statistics for monitoring.
        /// </summary>
        public (bool IsHealthy, string State, DateTime? LastFailure) GetHealthStatus()
        {
            var state = GetCircuitState();
            var isHealthy = state == CircuitState.Closed || state == CircuitState.HalfOpen;
            
            // Note: LastFailure would need to be tracked separately if needed
            return (isHealthy, state.ToString(), null);
        }
    }
}