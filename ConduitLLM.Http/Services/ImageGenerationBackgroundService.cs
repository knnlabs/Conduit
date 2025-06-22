using System;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Background service that processes image generation tasks from the Redis queue.
    /// Runs in each Core API instance to enable distributed processing.
    /// </summary>
    public class ImageGenerationBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ImageGenerationBackgroundService> _logger;
        private readonly string _instanceId;
        private readonly SemaphoreSlim _concurrencyLimiter;

        public ImageGenerationBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<ImageGenerationBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _instanceId = $"conduit-{Environment.MachineName}-{Guid.NewGuid():N}";
            
            // Limit concurrent image generations per instance
            var maxConcurrency = Environment.GetEnvironmentVariable("CONDUITLLM__IMAGEGENERATION__MAXCONCURRENCY") ?? "3";
            _concurrencyLimiter = new SemaphoreSlim(int.Parse(maxConcurrency));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Image generation background service starting on instance {InstanceId}", _instanceId);

            // Run periodic orphan recovery
            _ = Task.Run(async () => await RunOrphanRecoveryAsync(stoppingToken), stoppingToken);

            // Process tasks continuously
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _concurrencyLimiter.WaitAsync(stoppingToken);
                    
                    // Process next task without blocking
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await ProcessNextTaskAsync(stoppingToken);
                        }
                        finally
                        {
                            _concurrencyLimiter.Release();
                        }
                    }, stoppingToken);
                    
                    // Small delay to prevent tight loop when queue is empty
                    await Task.Delay(100, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Expected when shutting down
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in image generation background service loop");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }

            _logger.LogInformation("Image generation background service stopping on instance {InstanceId}", _instanceId);
        }

        private async Task ProcessNextTaskAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var queue = scope.ServiceProvider.GetRequiredService<IImageGenerationQueue>();
            var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

            try
            {
                // Try to dequeue a task
                var task = await queue.DequeueAsync(_instanceId, cancellationToken);
                if (task == null)
                {
                    // No tasks available
                    return;
                }

                _logger.LogInformation("Instance {InstanceId} processing image generation task {TaskId}", 
                    _instanceId, task.TaskId);

                // Create a cancellation token that combines the service token and a timeout
                using var taskCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                taskCts.CancelAfter(TimeSpan.FromMinutes(5)); // Max 5 minutes per task

                // Start heartbeat task to keep claim alive
                var heartbeatTask = Task.Run(async () =>
                {
                    while (!taskCts.Token.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30), taskCts.Token);
                        await queue.ExtendClaimAsync(task.TaskId, _instanceId, TimeSpan.FromMinutes(5), taskCts.Token);
                    }
                }, taskCts.Token);

                try
                {
                    // Publish the task to MassTransit for processing
                    // This allows us to reuse the existing ImageGenerationOrchestrator
                    await publishEndpoint.Publish(task, taskCts.Token);
                    
                    // Wait a bit for processing to complete
                    // In a more sophisticated implementation, we'd track completion via events
                    await Task.Delay(TimeSpan.FromSeconds(10), taskCts.Token);
                    
                    // Acknowledge the task
                    await queue.AcknowledgeAsync(task.TaskId, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing image generation task {TaskId}", task.TaskId);
                    
                    // Return task to queue for retry
                    var retryDelay = GetRetryDelay(ex);
                    await queue.ReturnToQueueAsync(task.TaskId, ex.Message, retryDelay, cancellationToken);
                }
                finally
                {
                    taskCts.Cancel();
                    try { await heartbeatTask; } catch { }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in task processing for instance {InstanceId}", _instanceId);
            }
        }

        private async Task RunOrphanRecoveryAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
                    
                    using var scope = _serviceProvider.CreateScope();
                    var queue = scope.ServiceProvider.GetRequiredService<IImageGenerationQueue>();
                    
                    var recovered = await queue.RecoverOrphanedTasksAsync(TimeSpan.FromMinutes(10), cancellationToken);
                    if (recovered > 0)
                    {
                        _logger.LogInformation("Recovered {Count} orphaned image generation tasks", recovered);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in orphan recovery task");
                }
            }
        }

        private TimeSpan GetRetryDelay(Exception ex)
        {
            // Implement exponential backoff based on error type
            return ex switch
            {
                TaskCanceledException => TimeSpan.FromSeconds(10),
                TimeoutException => TimeSpan.FromSeconds(30),
                _ when ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) => TimeSpan.FromMinutes(1),
                _ when ex.Message.Contains("quota", StringComparison.OrdinalIgnoreCase) => TimeSpan.FromMinutes(5),
                _ => TimeSpan.FromSeconds(30)
            };
        }

        public override void Dispose()
        {
            _concurrencyLimiter?.Dispose();
            base.Dispose();
        }
    }
}