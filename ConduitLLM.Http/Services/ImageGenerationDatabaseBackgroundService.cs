using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Configuration.Services;
using ConduitLLM.Core.Configuration;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Background service that processes image generation tasks from the database with multi-worker pattern
    /// </summary>
    public class ImageGenerationDatabaseBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IOptions<ImageGenerationRetryConfiguration> _retryConfig;
        private readonly ILogger<ImageGenerationDatabaseBackgroundService> _logger;
        private readonly string _instanceId = Guid.NewGuid().ToString("N")[..8];
        private readonly List<Task> _workers = new();

        // Worker intervals
        private readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(5);
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);
        private readonly TimeSpan _metricsInterval = TimeSpan.FromMinutes(1);
        private readonly TimeSpan _healthCheckInterval = TimeSpan.FromSeconds(30);
        private readonly TimeSpan _leaseRecoveryInterval = TimeSpan.FromMinutes(2);
        private readonly TimeSpan _retrySchedulerInterval = TimeSpan.FromSeconds(30);

        // Task metrics
        private long _tasksProcessed = 0;
        private long _tasksSucceeded = 0;
        private long _tasksFailed = 0;
        private long _tasksRetried = 0;
        private DateTime _serviceStartTime = DateTime.UtcNow;

        public ImageGenerationDatabaseBackgroundService(
            IServiceProvider serviceProvider,
            IOptions<ImageGenerationRetryConfiguration> retryConfig,
            ILogger<ImageGenerationDatabaseBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _retryConfig = retryConfig;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting Image Generation Database Background Service (Instance: {InstanceId})", _instanceId);
            _serviceStartTime = DateTime.UtcNow;

            // Start all workers
            _workers.Add(RunImageGenerationWorkerAsync(stoppingToken));
            _workers.Add(RunCleanupWorkerAsync(stoppingToken));
            _workers.Add(RunMetricsWorkerAsync(stoppingToken));
            _workers.Add(RunHealthCheckWorkerAsync(stoppingToken));
            _workers.Add(RunLeaseRecoveryWorkerAsync(stoppingToken));
            _workers.Add(RunRetrySchedulerWorkerAsync(stoppingToken));

            // Wait for all workers to complete
            await Task.WhenAll(_workers);

            _logger.LogInformation("Image Generation Database Background Service stopped (Instance: {InstanceId})", _instanceId);
        }

        /// <summary>
        /// Main worker that processes image generation tasks
        /// </summary>
        private async Task RunImageGenerationWorkerAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting image generation worker");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var repository = scope.ServiceProvider.GetRequiredService<IAsyncTaskRepository>();
                    var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

                    // Try to lease a pending task
                    var task = await repository.LeaseNextPendingTaskAsync(
                        workerId: _instanceId,
                        leaseDuration: TimeSpan.FromMinutes(10),
                        taskType: "image_generation");

                    if (task != null)
                    {
                        _logger.LogInformation("Leased image generation task {TaskId} for processing", task.Id);
                        Interlocked.Increment(ref _tasksProcessed);

                        try
                        {
                            // Update task to processing state
                            task.State = (int)TaskState.Processing;
                            task.UpdatedAt = DateTime.UtcNow;
                            await repository.UpdateAsync(task);

                            // Extract payload from metadata
                            string? payload = null;
                            if (!string.IsNullOrEmpty(task.Metadata))
                            {
                                try
                                {
                                    var metadata = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(task.Metadata);
                                    if (metadata != null && metadata.TryGetValue("payload", out var payloadObj))
                                    {
                                        payload = payloadObj.ToString();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Failed to extract payload from metadata for task {TaskId}", task.Id);
                                }
                            }

                            if (string.IsNullOrEmpty(payload))
                            {
                                throw new InvalidOperationException("No payload found in task metadata");
                            }

                            // Deserialize the request
                            var request = System.Text.Json.JsonSerializer.Deserialize<ImageGenerationRequested>(payload);
                            if (request == null)
                            {
                                throw new InvalidOperationException("Failed to deserialize task payload");
                            }

                            // Update the task ID in the request if needed
                            if (request.TaskId != task.Id)
                            {
                                request = request with { TaskId = task.Id };
                            }

                            // Publish to MassTransit for orchestration
                            await publishEndpoint.Publish(request, cancellationToken);

                            _logger.LogInformation("Published image generation request for task {TaskId}", task.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing image generation task {TaskId}", task.Id);
                            
                            // Handle retry logic
                            await HandleTaskFailureAsync(task, ex, scope.ServiceProvider, cancellationToken);
                        }
                    }
                    else
                    {
                        // No tasks available, wait before checking again
                        await Task.Delay(_processingInterval, cancellationToken);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in image generation worker");
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                }
            }

            _logger.LogInformation("Image generation worker stopped");
        }

        /// <summary>
        /// Worker that cleans up old completed/failed tasks
        /// </summary>
        private async Task RunCleanupWorkerAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting cleanup worker");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_cleanupInterval, cancellationToken);

                    using var scope = _serviceProvider.CreateScope();
                    var repository = scope.ServiceProvider.GetRequiredService<IAsyncTaskRepository>();

                    // Archive tasks older than 7 days
                    var archivedCount = await repository.ArchiveOldTasksAsync(TimeSpan.FromDays(7));

                    if (archivedCount > 0)
                    {
                        _logger.LogInformation("Archived {Count} old image generation tasks", archivedCount);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in cleanup worker");
                }
            }

            _logger.LogInformation("Cleanup worker stopped");
        }

        /// <summary>
        /// Worker that collects and logs metrics
        /// </summary>
        private async Task RunMetricsWorkerAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting metrics worker");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_metricsInterval, cancellationToken);

                    using var scope = _serviceProvider.CreateScope();
                    var repository = scope.ServiceProvider.GetRequiredService<IAsyncTaskRepository>();

                    // Get pending tasks to estimate queue depth
                    var pendingTasks = await repository.GetPendingTasksAsync("image_generation", limit: 1);
                    var uptime = DateTime.UtcNow - _serviceStartTime;

                    _logger.LogInformation(
                        "Image Generation Metrics - Instance: {InstanceId}, Uptime: {Uptime}, " +
                        "Processed: {Processed}, Succeeded: {Succeeded}, Failed: {Failed}, Retried: {Retried}",
                        _instanceId, uptime, _tasksProcessed, _tasksSucceeded, _tasksFailed, _tasksRetried);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in metrics worker");
                }
            }

            _logger.LogInformation("Metrics worker stopped");
        }

        /// <summary>
        /// Worker that monitors storage health
        /// </summary>
        private async Task RunHealthCheckWorkerAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting health check worker");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_healthCheckInterval, cancellationToken);

                    // Health checks could be implemented here if needed
                    // For now, just log that we're still running
                    _logger.LogDebug("Health check worker running - Instance: {InstanceId}", _instanceId);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in health check worker");
                }
            }

            _logger.LogInformation("Health check worker stopped");
        }

        /// <summary>
        /// Worker that recovers tasks with expired leases
        /// </summary>
        private async Task RunLeaseRecoveryWorkerAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting lease recovery worker");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_leaseRecoveryInterval, cancellationToken);

                    using var scope = _serviceProvider.CreateScope();
                    var repository = scope.ServiceProvider.GetRequiredService<IAsyncTaskRepository>();

                    // Get tasks with expired leases
                    var expiredTasks = await repository.GetExpiredLeaseTasksAsync(limit: 10);
                    var recoveredCount = 0;

                    foreach (var task in expiredTasks.Where(t => t.Type == "image_generation"))
                    {
                        // Reset the lease
                        task.LeasedBy = null;
                        task.LeaseExpiryTime = null;
                        task.UpdatedAt = DateTime.UtcNow;
                        
                        await repository.UpdateAsync(task);
                        recoveredCount++;
                    }

                    if (recoveredCount > 0)
                    {
                        _logger.LogInformation("Recovered {Count} image generation tasks with expired leases", 
                            recoveredCount);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in lease recovery worker");
                }
            }

            _logger.LogInformation("Lease recovery worker stopped");
        }

        /// <summary>
        /// Worker that checks for tasks ready to retry
        /// </summary>
        private async Task RunRetrySchedulerWorkerAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting retry scheduler worker");

            if (!_retryConfig.Value.EnableRetries)
            {
                _logger.LogInformation("Retries are disabled, retry scheduler worker will not run");
                return;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_retryConfig.Value.RetryCheckIntervalSeconds), 
                        cancellationToken);

                    using var scope = _serviceProvider.CreateScope();
                    var repository = scope.ServiceProvider.GetRequiredService<IAsyncTaskRepository>();

                    // Get pending tasks that might be ready for retry
                    var pendingTasks = await repository.GetPendingTasksAsync("image_generation", limit: 50);
                    var now = DateTime.UtcNow;

                    foreach (var task in pendingTasks)
                    {
                        // Check if this task has a NextRetryAt time and it's ready
                        if (task.NextRetryAt.HasValue && task.NextRetryAt.Value <= now)
                        {
                            _logger.LogInformation("Marking task {TaskId} as ready for retry attempt {RetryCount}", 
                                task.Id, task.RetryCount + 1);

                            // Clear NextRetryAt so it can be picked up by the main worker
                            task.NextRetryAt = null;
                            task.UpdatedAt = DateTime.UtcNow;
                            
                            await repository.UpdateAsync(task);
                            Interlocked.Increment(ref _tasksRetried);
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in retry scheduler worker");
                }
            }

            _logger.LogInformation("Retry scheduler worker stopped");
        }

        /// <summary>
        /// Handle task failure with retry logic
        /// </summary>
        private async Task HandleTaskFailureAsync(
            AsyncTask task, 
            Exception exception, 
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken)
        {
            var repository = serviceProvider.GetRequiredService<IAsyncTaskRepository>();
            var publishEndpoint = serviceProvider.GetRequiredService<IPublishEndpoint>();

            task.RetryCount++;
            var isRetryable = IsRetryableError(exception) && 
                              task.RetryCount < _retryConfig.Value.MaxRetries &&
                              _retryConfig.Value.EnableRetries;

            if (isRetryable)
            {
                // Calculate next retry time
                var retryDelay = _retryConfig.Value.CalculateRetryDelay(task.RetryCount);
                task.NextRetryAt = DateTime.UtcNow.AddSeconds(retryDelay);
                task.State = (int)TaskState.Pending; // Back to pending for retry
                task.LeasedBy = null;
                task.LeaseExpiryTime = null;

                _logger.LogWarning(
                    "Task {TaskId} failed with retryable error. Retry {RetryCount}/{MaxRetries} scheduled at {NextRetryAt}",
                    task.Id, task.RetryCount, _retryConfig.Value.MaxRetries, task.NextRetryAt);
            }
            else
            {
                // Mark as permanently failed
                task.State = (int)TaskState.Failed;
                task.Error = exception.Message;
                task.CompletedAt = DateTime.UtcNow;
                Interlocked.Increment(ref _tasksFailed);

                _logger.LogError("Task {TaskId} permanently failed after {RetryCount} attempts", 
                    task.Id, task.RetryCount);
            }

            task.UpdatedAt = DateTime.UtcNow;
            await repository.UpdateAsync(task);

            // Publish failure event
            if (task.State == (int)TaskState.Failed)
            {
                try
                {
                    // Extract payload from metadata
                    string? payload = null;
                    if (!string.IsNullOrEmpty(task.Metadata))
                    {
                        var metadata = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(task.Metadata);
                        if (metadata != null && metadata.TryGetValue("payload", out var payloadObj))
                        {
                            payload = payloadObj.ToString();
                        }
                    }

                    if (!string.IsNullOrEmpty(payload))
                    {
                        var request = System.Text.Json.JsonSerializer.Deserialize<ImageGenerationRequested>(payload);
                        if (request != null)
                        {
                            await publishEndpoint.Publish(new ImageGenerationFailed
                            {
                                TaskId = task.Id,
                                VirtualKeyId = request.VirtualKeyId,
                                Error = exception.Message,
                                ErrorCode = exception.GetType().Name,
                                Provider = request.Request.Model ?? "unknown",
                                IsRetryable = false,
                                AttemptCount = task.RetryCount,
                                CorrelationId = request.CorrelationId
                            }, cancellationToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish failure event for task {TaskId}", task.Id);
                }
            }
        }

        /// <summary>
        /// Determine if an error is retryable
        /// </summary>
        private bool IsRetryableError(Exception ex)
        {
            // Check exception types
            if (ex is TimeoutException || 
                ex is TaskCanceledException ||
                ex is HttpRequestException ||
                ex is System.IO.IOException ||
                ex is System.Net.Sockets.SocketException)
            {
                return true;
            }

            // Check error messages
            var message = ex.Message?.ToLowerInvariant() ?? string.Empty;
            var retryableKeywords = new[] 
            { 
                "timeout", "connection", "network", "temporarily unavailable", 
                "service unavailable", "too many requests", "rate limit"
            };

            return retryableKeywords.Any(keyword => message.Contains(keyword));
        }
    }
}