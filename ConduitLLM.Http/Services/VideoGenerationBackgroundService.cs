using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
                RunHealthMonitoringAsync(stoppingToken)
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
            _logger.LogInformation("Video generation worker started");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var asyncTaskService = scope.ServiceProvider.GetRequiredService<IAsyncTaskService>();
                    var publishEndpoint = scope.ServiceProvider.GetService<IPublishEndpoint>();

                    // Get pending video generation tasks
                    var pendingTasks = await asyncTaskService.GetPendingTasksAsync("video_generation", limit: 10, cancellationToken);

                    if (pendingTasks.Any())
                    {
                        _logger.LogDebug("Found {Count} pending video generation tasks", pendingTasks.Count);

                        foreach (var task in pendingTasks)
                        {
                            if (cancellationToken.IsCancellationRequested)
                                break;

                            try
                            {
                                _logger.LogInformation("Processing video generation task {TaskId}", task.TaskId);

                                // Update task status to processing
                                await asyncTaskService.UpdateTaskStatusAsync(
                                    task.TaskId, 
                                    TaskState.Processing, 
                                    cancellationToken: cancellationToken);

                                // Reconstruct the video generation request from task metadata
                                var videoRequest = await CreateVideoGenerationRequestFromTask(task);
                                
                                // Publish VideoGenerationRequested event for async processing
                                if (publishEndpoint != null)
                                {
                                    await publishEndpoint.Publish(videoRequest, cancellationToken);
                                    _logger.LogInformation("Published VideoGenerationRequested event for task {TaskId}", task.TaskId);
                                }
                                else
                                {
                                    _logger.LogWarning("No publish endpoint available, marking task {TaskId} as failed", task.TaskId);
                                    await asyncTaskService.UpdateTaskStatusAsync(
                                        task.TaskId,
                                        TaskState.Failed,
                                        error: "Event bus not available",
                                        cancellationToken: cancellationToken);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error processing video generation task {TaskId}", task.TaskId);
                                
                                // Mark task as failed
                                try
                                {
                                    await asyncTaskService.UpdateTaskStatusAsync(
                                        task.TaskId,
                                        TaskState.Failed,
                                        error: ex.Message,
                                        cancellationToken: cancellationToken);
                                }
                                catch (Exception updateEx)
                                {
                                    _logger.LogError(updateEx, "Failed to update task status for {TaskId}", task.TaskId);
                                }
                            }
                        }
                    }

                    // Wait before checking for more tasks
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
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
    }
}