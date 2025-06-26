using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Http.Hubs;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Implementation of task notification service that sends real-time updates through SignalR.
    /// </summary>
    public class TaskNotificationService : ITaskNotificationService
    {
        private readonly IHubContext<TaskHub> _taskHubContext;
        private readonly IHubContext<ImageGenerationHub>? _imageHubContext;
        private readonly IHubContext<VideoGenerationHub>? _videoHubContext;
        private readonly ILogger<TaskNotificationService> _logger;

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
        }

        public async Task NotifyTaskStartedAsync(string taskId, string taskType, int virtualKeyId, object? metadata = null)
        {
            try
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify task started for {TaskId}", taskId);
            }
        }

        public async Task NotifyTaskProgressAsync(string taskId, int progress, string? message = null)
        {
            try
            {
                // Send to unified TaskHub
                await _taskHubContext.Clients.Group($"task-{taskId}")
                    .SendAsync("TaskProgress", taskId, progress, message);

                // Send to legacy hubs for backward compatibility
                await SendToLegacyHubForTask(taskId, "TaskProgress", taskId, progress, message ?? string.Empty);

                _logger.LogDebug("Notified task progress: {TaskId} at {Progress}% - {Message}",
                    taskId, progress, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify task progress for {TaskId}", taskId);
            }
        }

        public async Task NotifyTaskCompletedAsync(string taskId, object? result = null)
        {
            try
            {
                // Send to unified TaskHub
                await _taskHubContext.Clients.Group($"task-{taskId}")
                    .SendAsync("TaskCompleted", taskId, result);

                // Send to legacy hubs for backward compatibility
                await SendToLegacyHubForTask(taskId, "TaskCompleted", taskId, result ?? new object());

                _logger.LogDebug("Notified task completed: {TaskId}", taskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify task completed for {TaskId}", taskId);
            }
        }

        public async Task NotifyTaskFailedAsync(string taskId, string error, bool isRetryable = false)
        {
            try
            {
                // Send to unified TaskHub
                await _taskHubContext.Clients.Group($"task-{taskId}")
                    .SendAsync("TaskFailed", taskId, error, isRetryable);

                // Send to legacy hubs for backward compatibility
                await SendToLegacyHubForTask(taskId, "TaskFailed", taskId, error, isRetryable);

                _logger.LogDebug("Notified task failed: {TaskId} with error: {Error} (Retryable: {IsRetryable})",
                    taskId, error, isRetryable);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify task failed for {TaskId}", taskId);
            }
        }

        public async Task NotifyTaskCancelledAsync(string taskId, string? reason = null)
        {
            try
            {
                // Send to unified TaskHub
                await _taskHubContext.Clients.Group($"task-{taskId}")
                    .SendAsync("TaskCancelled", taskId, reason);

                // Send to legacy hubs for backward compatibility
                await SendToLegacyHubForTask(taskId, "TaskCancelled", taskId, reason ?? string.Empty);

                _logger.LogDebug("Notified task cancelled: {TaskId} with reason: {Reason}",
                    taskId, reason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify task cancelled for {TaskId}", taskId);
            }
        }

        public async Task NotifyTaskTimedOutAsync(string taskId, int timeoutSeconds)
        {
            try
            {
                // Send to unified TaskHub
                await _taskHubContext.Clients.Group($"task-{taskId}")
                    .SendAsync("TaskTimedOut", taskId, timeoutSeconds);

                // Send to legacy hubs for backward compatibility
                await SendToLegacyHubForTask(taskId, "TaskTimedOut", taskId, timeoutSeconds);

                _logger.LogDebug("Notified task timed out: {TaskId} after {TimeoutSeconds} seconds",
                    taskId, timeoutSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify task timed out for {TaskId}", taskId);
            }
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
            }
        }
    }
}