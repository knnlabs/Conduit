using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Service for tracking task processing metrics including queue depths,
    /// processing times, and success/failure rates.
    /// </summary>
    public class TaskProcessingMetricsService : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<TaskProcessingMetricsService> _logger;
        private readonly TimeSpan _collectionInterval = TimeSpan.FromSeconds(30);

        // Task queue metrics
        private static readonly Gauge TaskQueueDepth = Prometheus.Metrics
            .CreateGauge("conduit_tasks_queue_depth", "Number of tasks in queue",
                new GaugeConfiguration
                {
                    LabelNames = new[] { "task_type", "status" }
                });

        private static readonly Histogram TaskProcessingDuration = Prometheus.Metrics
            .CreateHistogram("conduit_task_processing_duration_seconds", "Task processing duration in seconds",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "task_type", "provider", "status" },
                    Buckets = Histogram.ExponentialBuckets(0.1, 2, 16) // 0.1s to ~1 hour
                });

        private static readonly Counter TasksCompleted = Prometheus.Metrics
            .CreateCounter("conduit_tasks_completed_total", "Total number of completed tasks",
                new CounterConfiguration
                {
                    LabelNames = new[] { "task_type", "provider", "status" }
                });

        private static readonly Counter TaskRetries = Prometheus.Metrics
            .CreateCounter("conduit_task_retries_total", "Total number of task retries",
                new CounterConfiguration
                {
                    LabelNames = new[] { "task_type", "provider" }
                });

        private static readonly Gauge TasksInProgress = Prometheus.Metrics
            .CreateGauge("conduit_tasks_in_progress", "Number of tasks currently being processed",
                new GaugeConfiguration
                {
                    LabelNames = new[] { "task_type", "provider" }
                });

        private static readonly Summary TaskWaitTime = Prometheus.Metrics
            .CreateSummary("conduit_task_wait_time_seconds", "Time tasks spend waiting in queue",
                new SummaryConfiguration
                {
                    LabelNames = new[] { "task_type" },
                    Objectives = new[]
                    {
                        new QuantileEpsilonPair(0.5, 0.05),
                        new QuantileEpsilonPair(0.9, 0.01),
                        new QuantileEpsilonPair(0.95, 0.005),
                        new QuantileEpsilonPair(0.99, 0.001)
                    },
                    MaxAge = TimeSpan.FromMinutes(5),
                    AgeBuckets = 5
                });

        private static readonly Counter WebhookDeliveries = Prometheus.Metrics
            .CreateCounter("conduit_webhook_deliveries_total", "Total number of webhook delivery attempts",
                new CounterConfiguration
                {
                    LabelNames = new[] { "status", "retry_count" }
                });

        private static readonly Histogram WebhookDeliveryDuration = Prometheus.Metrics
            .CreateHistogram("conduit_webhook_delivery_duration_seconds", "Webhook delivery duration",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "status" },
                    Buckets = Histogram.ExponentialBuckets(0.01, 2, 14) // 10ms to ~82s
                });

        private static readonly Gauge VirtualKeySpendRate = Prometheus.Metrics
            .CreateGauge("conduit_virtualkey_spend_rate", "Virtual key spend rate per minute",
                new GaugeConfiguration
                {
                    LabelNames = new[] { "virtual_key_id" }
                });

        private static readonly Counter TaskCancellations = Prometheus.Metrics
            .CreateCounter("conduit_task_cancellations_total", "Total number of task cancellations",
                new CounterConfiguration
                {
                    LabelNames = new[] { "task_type", "reason" }
                });

        private static readonly Histogram BatchProcessingSize = Prometheus.Metrics
            .CreateHistogram("conduit_batch_processing_size", "Number of items processed in batch operations",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "operation_type" },
                    Buckets = new[] { 1.0, 5.0, 10.0, 25.0, 50.0, 100.0, 250.0, 500.0, 1000.0 }
                });

        public TaskProcessingMetricsService(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<TaskProcessingMetricsService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Task processing metrics service starting...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CollectMetricsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error collecting task processing metrics");
                }

                await Task.Delay(_collectionInterval, stoppingToken);
            }

            _logger.LogInformation("Task processing metrics service stopped");
        }

        private async Task CollectMetricsAsync()
        {
            using var scope = _serviceScopeFactory.CreateScope();

            // Collect async task metrics
            await CollectAsyncTaskMetrics(scope);

            // Collect image generation task metrics
            await CollectImageGenerationMetrics(scope);

            // Collect video generation task metrics
            await CollectVideoGenerationMetrics(scope);

            // Collect virtual key spend metrics
            await CollectVirtualKeySpendMetrics(scope);
        }

        private async Task CollectAsyncTaskMetrics(IServiceScope scope)
        {
            try
            {
                var taskService = scope.ServiceProvider.GetService<IAsyncTaskService>();
                if (taskService == null) return;

                // TODO: Implement task metrics collection when GetAllTasksAsync is available
                // For now, we'll skip this as the IAsyncTaskService doesn't expose a method
                // to get all tasks. This would need to be added to the interface.
                
                await Task.CompletedTask; // Keep async signature
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting async task metrics");
            }
        }

        private async Task CollectImageGenerationMetrics(IServiceScope scope)
        {
            try
            {
                var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ConduitLLM.Configuration.ConduitDbContext>>();
                await using var context = await dbContextFactory.CreateDbContextAsync();

                // Get image generation task statistics from AsyncTasks
                var now = DateTime.UtcNow;
                var oneHourAgo = now.AddHours(-1);

                var imageStats = await context.AsyncTasks
                    .Where(t => t.Type == "image_generation" && t.CreatedAt >= oneHourAgo)
                    .GroupBy(t => new { State = t.State })
                    .Select(g => new
                    {
                        State = g.Key.State,
                        Count = g.Count(),
                        AvgDuration = g.Where(t => t.CompletedAt.HasValue).Count() > 0 
                            ? g.Where(t => t.CompletedAt.HasValue)
                                .Average(t => (double)((t.CompletedAt!.Value - t.CreatedAt).TotalSeconds))
                            : (double?)null
                    })
                    .ToListAsync();

                foreach (var stat in imageStats)
                {
                    var status = GetStatusFromState(stat.State);
                    if (status == "processing")
                    {
                        TasksInProgress.WithLabels("image", "unknown").Set(stat.Count);
                    }

                    if (stat.AvgDuration.HasValue)
                    {
                        TaskProcessingDuration.WithLabels("image", "unknown", status)
                            .Observe(stat.AvgDuration.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting image generation metrics");
            }
        }

        private async Task CollectVideoGenerationMetrics(IServiceScope scope)
        {
            try
            {
                var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ConduitLLM.Configuration.ConduitDbContext>>();
                await using var context = await dbContextFactory.CreateDbContextAsync();

                // Get video generation task statistics from AsyncTasks
                var now = DateTime.UtcNow;
                var oneHourAgo = now.AddHours(-1);

                var videoStats = await context.AsyncTasks
                    .Where(t => t.Type == "video_generation" && t.CreatedAt >= oneHourAgo)
                    .GroupBy(t => new { State = t.State })
                    .Select(g => new
                    {
                        State = g.Key.State,
                        Count = g.Count(),
                        AvgDuration = g.Where(t => t.CompletedAt.HasValue).Count() > 0 
                            ? g.Where(t => t.CompletedAt.HasValue)
                                .Average(t => (double)((t.CompletedAt!.Value - t.CreatedAt).TotalSeconds))
                            : (double?)null
                    })
                    .ToListAsync();

                foreach (var stat in videoStats)
                {
                    var status = GetStatusFromState(stat.State);
                    if (status == "processing")
                    {
                        TasksInProgress.WithLabels("video", "unknown").Set(stat.Count);
                    }

                    if (stat.AvgDuration.HasValue)
                    {
                        TaskProcessingDuration.WithLabels("video", "unknown", status)
                            .Observe(stat.AvgDuration.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting video generation metrics");
            }
        }

        private static string GetStatusFromState(int state)
        {
            // AsyncTaskState enum: 0=Pending, 1=Processing, 2=Completed, 3=Failed, 4=Cancelled
            return state switch
            {
                0 => "pending",
                1 => "processing",
                2 => "completed",
                3 => "failed",
                4 => "cancelled",
                _ => "unknown"
            };
        }

        private async Task CollectVirtualKeySpendMetrics(IServiceScope scope)
        {
            try
            {
                var spendHistoryRepo = scope.ServiceProvider.GetRequiredService<IVirtualKeySpendHistoryRepository>();
                
                // Get spend rate for top virtual keys in the last minute
                var now = DateTime.UtcNow;
                var oneMinuteAgo = now.AddMinutes(-1);
                var recentSpends = await spendHistoryRepo.GetByDateRangeAsync(oneMinuteAgo, now);

                var spendByKey = recentSpends
                    .GroupBy(s => s.VirtualKeyId)
                    .Select(g => new
                    {
                        VirtualKeyId = g.Key,
                        TotalSpend = g.Sum(s => s.Amount)
                    })
                    .OrderByDescending(s => s.TotalSpend)
                    .Take(100); // Top 100 spenders

                foreach (var spend in spendByKey)
                {
                    VirtualKeySpendRate.WithLabels(spend.VirtualKeyId.ToString()).Set((double)spend.TotalSpend);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting virtual key spend metrics");
            }
        }

        // Static methods to be called by task processing code
        public static void RecordTaskStarted(string taskType, string provider)
        {
            TasksInProgress.WithLabels(taskType, provider).Inc();
        }

        public static void RecordTaskCompleted(string taskType, string provider, string status, double durationSeconds)
        {
            TasksInProgress.WithLabels(taskType, provider).Dec();
            TasksCompleted.WithLabels(taskType, provider, status).Inc();
            TaskProcessingDuration.WithLabels(taskType, provider, status).Observe(durationSeconds);
        }

        public static void RecordTaskRetry(string taskType, string provider)
        {
            TaskRetries.WithLabels(taskType, provider).Inc();
        }

        public static void RecordTaskCancellation(string taskType, string reason)
        {
            TaskCancellations.WithLabels(taskType, reason).Inc();
        }

        public static void RecordWebhookDelivery(string status, int retryCount, double durationSeconds)
        {
            WebhookDeliveries.WithLabels(status, retryCount.ToString()).Inc();
            WebhookDeliveryDuration.WithLabels(status).Observe(durationSeconds);
        }

        public static void RecordBatchProcessing(string operationType, int batchSize)
        {
            BatchProcessingSize.WithLabels(operationType).Observe(batchSize);
        }
    }
}