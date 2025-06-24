using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Background service that processes video generation tasks from the queue.
    /// Implements a proper worker pattern for scalable async task processing.
    /// </summary>
    public class VideoGenerationBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<VideoGenerationBackgroundService> _logger;
        private readonly string _instanceId;

        public VideoGenerationBackgroundService(
            IServiceProvider serviceProvider,
            IMemoryCache memoryCache,
            ILogger<VideoGenerationBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _memoryCache = memoryCache;
            _logger = logger;
            _instanceId = $"conduit-video-{Environment.MachineName}-{Guid.NewGuid():N}";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Video generation background service started on instance {InstanceId}", _instanceId);

            // Run multiple background tasks concurrently
            var tasks = new[]
            {
                RunVideoGenerationWorkerAsync(stoppingToken),
                RunTaskCleanupAsync(stoppingToken),
                RunMetricsCollectionAsync(stoppingToken),
                RunHealthMonitoringAsync(stoppingToken),
                RunExpiredLeaseRecoveryAsync(stoppingToken),
                RunRetrySchedulerAsync(stoppingToken)
            };

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected when shutting down
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error in video generation background service");
            }

            _logger.LogInformation("Video generation background service stopped on instance {InstanceId}", _instanceId);
        }

        private async Task RunVideoGenerationWorkerAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Video generation worker started with instance ID {InstanceId}", _instanceId);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var asyncTaskRepository = scope.ServiceProvider.GetRequiredService<IAsyncTaskRepository>();
                    var asyncTaskService = scope.ServiceProvider.GetRequiredService<IAsyncTaskService>();
                    var distributedLockService = scope.ServiceProvider.GetService<IDistributedLockService>();
                    var publishEndpoint = scope.ServiceProvider.GetService<IPublishEndpoint>();

                    // Attempt to lease the next pending task (including retryable tasks)
                    var leasedTask = await asyncTaskRepository.LeaseNextPendingTaskAsync(
                        _instanceId,
                        TimeSpan.FromMinutes(10), // 10-minute lease
                        "video_generation",
                        cancellationToken);

                    if (leasedTask != null)
                    {
                        // Check if this is a retry task and if it's ready to be retried
                        if (leasedTask.NextRetryAt.HasValue && leasedTask.NextRetryAt.Value > DateTime.UtcNow)
                        {
                            // Task is not ready for retry yet, release the lease
                            await asyncTaskRepository.ReleaseLeaseAsync(leasedTask.Id, _instanceId, cancellationToken);
                            _logger.LogDebug("Task {TaskId} scheduled for retry at {NextRetryAt}, skipping", 
                                leasedTask.Id, leasedTask.NextRetryAt);
                            continue;
                        }

                        _logger.LogInformation("Worker {WorkerId} leased video generation task {TaskId} (Retry: {RetryCount}/{MaxRetries})", 
                            _instanceId, leasedTask.Id, leasedTask.RetryCount, leasedTask.MaxRetries);

                        try
                        {
                            // Convert database entity to AsyncTaskStatus
                            var taskStatus = await asyncTaskService.GetTaskStatusAsync(leasedTask.Id);
                            if (taskStatus == null)
                            {
                                _logger.LogWarning("Task status not found for leased task {TaskId}", leasedTask.Id);
                                continue;
                            }

                            // Increment retry count if this is a retry
                            if (leasedTask.RetryCount > 0)
                            {
                                leasedTask.RetryCount++;
                                leasedTask.NextRetryAt = null; // Clear next retry time
                                leasedTask.UpdatedAt = DateTime.UtcNow;
                                await asyncTaskRepository.UpdateAsync(leasedTask, cancellationToken);
                                
                                _logger.LogInformation("Processing retry attempt {RetryCount}/{MaxRetries} for task {TaskId}", 
                                    leasedTask.RetryCount, leasedTask.MaxRetries, leasedTask.Id);
                            }

                            // Update task status to processing
                            await asyncTaskService.UpdateTaskStatusAsync(
                                leasedTask.Id, 
                                TaskState.Processing, 
                                cancellationToken: cancellationToken);

                            // Reconstruct the video generation request from task metadata
                            var videoRequest = await CreateVideoGenerationRequestFromTask(taskStatus);
                            
                            // Publish VideoGenerationRequested event for async processing
                            if (publishEndpoint != null)
                            {
                                await publishEndpoint.Publish(videoRequest, cancellationToken);
                                _logger.LogInformation("Published VideoGenerationRequested event for task {TaskId}", leasedTask.Id);
                            }
                            else
                            {
                                _logger.LogWarning("No publish endpoint available, marking task {TaskId} as failed", leasedTask.Id);
                                await asyncTaskService.UpdateTaskStatusAsync(
                                    leasedTask.Id,
                                    TaskState.Failed,
                                    error: "Event bus not available",
                                    cancellationToken: cancellationToken);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing video generation task {TaskId}", leasedTask.Id);
                            
                            // Mark task as failed
                            try
                            {
                                await asyncTaskService.UpdateTaskStatusAsync(
                                    leasedTask.Id,
                                    TaskState.Failed,
                                    error: ex.Message,
                                    cancellationToken: cancellationToken);
                            }
                            catch (Exception updateEx)
                            {
                                _logger.LogError(updateEx, "Failed to update task status for {TaskId}", leasedTask.Id);
                            }
                        }
                        finally
                        {
                            // Release the lease if the task is completed or failed
                            var finalStatus = await asyncTaskService.GetTaskStatusAsync(leasedTask.Id);
                            if (finalStatus != null && 
                                (finalStatus.State == TaskState.Completed || 
                                 finalStatus.State == TaskState.Failed ||
                                 finalStatus.State == TaskState.Cancelled))
                            {
                                await asyncTaskRepository.ReleaseLeaseAsync(leasedTask.Id, _instanceId, cancellationToken);
                                _logger.LogDebug("Released lease for completed task {TaskId}", leasedTask.Id);
                            }
                        }
                    }
                    else
                    {
                        // No tasks available, wait a bit longer
                        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in video generation worker loop");
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }

            _logger.LogInformation("Video generation worker stopped");
        }

        private async Task<VideoGenerationRequested> CreateVideoGenerationRequestFromTask(AsyncTaskStatus task)
        {
            // Parse metadata to reconstruct the request
            var metadata = task.Metadata as System.Text.Json.JsonElement? ?? 
                          (task.Metadata != null ? System.Text.Json.JsonSerializer.SerializeToElement(task.Metadata) : default);

            // Extract webhook configuration
            string? webhookUrl = null;
            Dictionary<string, string>? webhookHeaders = null;
            
            if (metadata.TryGetProperty("WebhookUrl", out var webhookUrlElement) && webhookUrlElement.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                webhookUrl = webhookUrlElement.GetString();
            }

            if (metadata.TryGetProperty("WebhookHeaders", out var webhookHeadersElement) && webhookHeadersElement.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                webhookHeaders = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(webhookHeadersElement.GetRawText());
            }

            // Extract video generation parameters
            VideoGenerationParameters? parameters = null;
            if (metadata.TryGetProperty("Request", out var requestData))
            {
                parameters = new VideoGenerationParameters
                {
                    Size = requestData.TryGetProperty("size", out var size) ? size.GetString() : null,
                    Duration = requestData.TryGetProperty("duration", out var duration) && duration.TryGetInt32(out var dur) ? dur : null,
                    Fps = requestData.TryGetProperty("fps", out var fps) && fps.TryGetInt32(out var fpsVal) ? fpsVal : null,
                    Style = requestData.TryGetProperty("style", out var style) ? style.GetString() : null,
                    ResponseFormat = requestData.TryGetProperty("response_format", out var format) ? format.GetString() : null
                };
            }

            var request = new VideoGenerationRequested
            {
                RequestId = task.TaskId,
                Model = metadata.TryGetProperty("Model", out var model) ? model.GetString() ?? "" : "",
                Prompt = metadata.TryGetProperty("Prompt", out var prompt) ? prompt.GetString() ?? "" : "",
                VirtualKeyId = metadata.TryGetProperty("VirtualKey", out var vk) ? vk.GetString() ?? "" : "",
                IsAsync = true,
                RequestedAt = task.CreatedAt,
                CorrelationId = task.TaskId,
                WebhookUrl = webhookUrl,
                WebhookHeaders = webhookHeaders,
                Parameters = parameters
            };

            return request;
        }

        private async Task RunTaskCleanupAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Wait for cleanup interval (5 minutes)
                    await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);

                    using var scope = _serviceProvider.CreateScope();
                    var asyncTaskService = scope.ServiceProvider.GetRequiredService<IAsyncTaskService>();

                    _logger.LogDebug("Running video generation task cleanup");

                    // Clean up old completed or failed tasks (older than 24 hours)
                    var cleanedUp = await asyncTaskService.CleanupOldTasksAsync(TimeSpan.FromHours(24), cancellationToken);
                    
                    if (cleanedUp > 0)
                    {
                        _logger.LogInformation("Cleaned up {Count} old video generation tasks", cleanedUp);
                    }

                    // Clean up orphaned progress cache entries
                    CleanupOrphanedCacheEntries();
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during video generation task cleanup");
                    await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
                }
            }
        }

        private async Task RunMetricsCollectionAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Wait for metrics collection interval (1 minute)
                    await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);

                    // Collect and log metrics about video generation performance
                    var completedTasks = _memoryCache.Get<System.Collections.Generic.List<object>>("completed_video_tasks");
                    if (completedTasks != null && completedTasks.Any())
                    {
                        var recentTasks = completedTasks.Where(t => 
                        {
                            if (t is IDictionary<string, object> dict && dict.TryGetValue("CompletedAt", out var completedAt))
                            {
                                if (completedAt is DateTime dt)
                                {
                                    return dt > DateTime.UtcNow.AddMinutes(-5);
                                }
                            }
                            return false;
                        }).ToList();

                        if (recentTasks.Any())
                        {
                            LogAggregateMetrics(recentTasks);
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during video generation metrics collection");
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                }
            }
        }

        private async Task RunHealthMonitoringAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Wait for health check interval (30 seconds)
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

                    using var scope = _serviceProvider.CreateScope();
                    var mediaStorageService = scope.ServiceProvider.GetService<IMediaStorageService>();
                    
                    if (mediaStorageService != null)
                    {
                        // Check if media storage is healthy
                        try
                        {
                            // Simple health check - try to check if service is responsive
                            var testKey = $"health-check-{_instanceId}";
                            var isHealthy = await mediaStorageService.ExistsAsync(testKey);
                            
                            _logger.LogDebug("Video generation storage health check: {Status}", isHealthy ? "Healthy" : "Unknown");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Video generation storage health check failed");
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during video generation health monitoring");
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                }
            }
        }

        private void CleanupOrphanedCacheEntries()
        {
            try
            {
                // This is a simplified cleanup - in production, you'd want more sophisticated cache management
                var cacheEntriesToRemove = 0;
                
                // Log cleanup activity
                if (cacheEntriesToRemove > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} orphaned video generation cache entries", cacheEntriesToRemove);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cleaning up orphaned cache entries");
            }
        }

        private void LogAggregateMetrics(System.Collections.Generic.List<object> recentTasks)
        {
            try
            {
                var totalTasks = recentTasks.Count;
                var totalCost = 0m;
                var totalGenerationTime = 0.0;
                var providerCounts = new System.Collections.Generic.Dictionary<string, int>();

                foreach (var task in recentTasks)
                {
                    if (task is IDictionary<string, object> dict)
                    {
                        if (dict.TryGetValue("Cost", out var cost) && cost is decimal costValue)
                        {
                            totalCost += costValue;
                        }
                        
                        if (dict.TryGetValue("GenerationDuration", out var duration) && duration is double durationValue)
                        {
                            totalGenerationTime += durationValue;
                        }
                        
                        if (dict.TryGetValue("Provider", out var provider) && provider is string providerName)
                        {
                            if (!providerCounts.ContainsKey(providerName))
                                providerCounts[providerName] = 0;
                            providerCounts[providerName]++;
                        }
                    }
                }

                _logger.LogInformation("Video generation metrics (last 5 min): Tasks={TotalTasks}, TotalCost=${TotalCost:F2}, AvgGenerationTime={AvgTime:F1}s, Providers={Providers}",
                    totalTasks,
                    totalCost,
                    totalTasks > 0 ? totalGenerationTime / totalTasks : 0,
                    string.Join(", ", providerCounts.Select(kv => $"{kv.Key}:{kv.Value}")));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error calculating aggregate metrics");
            }
        }

        private async Task RunExpiredLeaseRecoveryAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Expired lease recovery task started");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Wait for recovery interval (2 minutes)
                    await Task.Delay(TimeSpan.FromMinutes(2), cancellationToken);

                    using var scope = _serviceProvider.CreateScope();
                    var asyncTaskRepository = scope.ServiceProvider.GetRequiredService<IAsyncTaskRepository>();
                    var asyncTaskService = scope.ServiceProvider.GetRequiredService<IAsyncTaskService>();

                    _logger.LogDebug("Checking for expired task leases");

                    // Get tasks with expired leases
                    var expiredTasks = await asyncTaskRepository.GetExpiredLeaseTasksAsync(100, cancellationToken);

                    if (expiredTasks.Any())
                    {
                        _logger.LogInformation("Found {Count} tasks with expired leases", expiredTasks.Count);

                        foreach (var task in expiredTasks)
                        {
                            try
                            {
                                // Reset the task back to pending state
                                task.State = 0; // Pending
                                task.LeasedBy = null;
                                task.LeaseExpiryTime = null;
                                task.UpdatedAt = DateTime.UtcNow;

                                await asyncTaskRepository.UpdateAsync(task, cancellationToken);

                                // Also update task status in cache
                                await asyncTaskService.UpdateTaskStatusAsync(
                                    task.Id, 
                                    TaskState.Pending,
                                    error: "Task lease expired and was reset",
                                    cancellationToken: cancellationToken);

                                _logger.LogInformation("Reset expired task {TaskId} back to pending state", task.Id);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error resetting expired task {TaskId}", task.Id);
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during expired lease recovery");
                    await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
                }
            }

            _logger.LogInformation("Expired lease recovery task stopped");
        }

        private async Task RunRetrySchedulerAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Retry scheduler task started");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Wait for retry check interval (30 seconds)
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

                    using var scope = _serviceProvider.CreateScope();
                    var asyncTaskRepository = scope.ServiceProvider.GetRequiredService<IAsyncTaskRepository>();

                    _logger.LogDebug("Checking for tasks ready to retry");

                    // Get pending tasks that have a NextRetryAt time in the past
                    var pendingTasks = await asyncTaskRepository.GetPendingTasksAsync("video_generation", 100, cancellationToken);
                    var tasksToRetry = pendingTasks
                        .Where(t => t.NextRetryAt.HasValue && t.NextRetryAt.Value <= DateTime.UtcNow)
                        .ToList();

                    if (tasksToRetry.Any())
                    {
                        _logger.LogInformation("Found {Count} tasks ready for retry", tasksToRetry.Count);

                        foreach (var task in tasksToRetry)
                        {
                            _logger.LogInformation("Task {TaskId} is ready for retry (attempt {RetryCount}/{MaxRetries})", 
                                task.Id, task.RetryCount + 1, task.MaxRetries);
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during retry scheduler task");
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                }
            }

            _logger.LogInformation("Retry scheduler task stopped");
        }
    }
}