using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;

using ConduitLLM.Admin.Interfaces;
namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Background service that collects error queue metrics for Prometheus.
    /// </summary>
    public class ErrorQueueMetricsService : BackgroundService
    {
        private readonly IRabbitMQManagementClient _rabbitClient;
        private readonly ILogger<ErrorQueueMetricsService> _logger;
        private readonly TimeSpan _collectionInterval = TimeSpan.FromSeconds(30);

        #region Prometheus Metrics

        // Counter Metrics
        private static readonly Counter ErrorQueueMessagesTotal = Metrics
            .CreateCounter("conduit_error_queue_messages_total",
                "Total number of messages moved to error queues",
                new CounterConfiguration
                {
                    LabelNames = new[] { "queue_name", "original_queue", "message_type" }
                });

        private static readonly Counter ErrorQueueMessagesReplayedTotal = Metrics
            .CreateCounter("conduit_error_queue_messages_replayed_total",
                "Total number of messages replayed from error queues",
                new CounterConfiguration
                {
                    LabelNames = new[] { "queue_name", "status" } // status: success, failed
                });

        private static readonly Counter ErrorQueueMessagesDeletedTotal = Metrics
            .CreateCounter("conduit_error_queue_messages_deleted_total",
                "Total number of messages deleted from error queues",
                new CounterConfiguration
                {
                    LabelNames = new[] { "queue_name", "reason" } // reason: manual, ttl, archived
                });

        // Gauge Metrics
        private static readonly Gauge ErrorQueueDepth = Metrics
            .CreateGauge("conduit_error_queue_depth",
                "Current number of messages in error queue",
                new GaugeConfiguration
                {
                    LabelNames = new[] { "queue_name", "original_queue" }
                });

        private static readonly Gauge ErrorQueueBytes = Metrics
            .CreateGauge("conduit_error_queue_bytes",
                "Current size of error queue in bytes",
                new GaugeConfiguration
                {
                    LabelNames = new[] { "queue_name" }
                });

        private static readonly Gauge ErrorQueueOldestMessageAge = Metrics
            .CreateGauge("conduit_error_queue_oldest_message_age_seconds",
                "Age of the oldest message in the error queue in seconds",
                new GaugeConfiguration
                {
                    LabelNames = new[] { "queue_name" }
                });

        // Histogram Metrics
        private static readonly Histogram ErrorQueueOperationDuration = Metrics
            .CreateHistogram("conduit_error_queue_operation_duration_seconds",
                "Time spent on error queue operations",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "operation", "queue_name" }, // operation: list, get_messages, replay, delete
                    Buckets = Histogram.ExponentialBuckets(0.001, 2, 10) // 1ms to ~1s
                });

        private static readonly Histogram ErrorQueueMessageSize = Metrics
            .CreateHistogram("conduit_error_queue_message_size_bytes",
                "Size distribution of messages in error queues",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "queue_name", "message_type" },
                    Buckets = Histogram.ExponentialBuckets(100, 2, 15) // 100 bytes to ~3.2MB
                });

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorQueueMetricsService"/> class.
        /// </summary>
        /// <param name="rabbitClient">RabbitMQ management client.</param>
        /// <param name="logger">Logger.</param>
        public ErrorQueueMetricsService(
            IRabbitMQManagementClient rabbitClient,
            ILogger<ErrorQueueMetricsService> logger)
        {
            _rabbitClient = rabbitClient ?? throw new ArgumentNullException(nameof(rabbitClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Executes the background service.
        /// </summary>
        /// <param name="stoppingToken">Cancellation token.</param>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Error Queue Metrics Service starting...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CollectMetrics(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error collecting error queue metrics");
                }

                await Task.Delay(_collectionInterval, stoppingToken);
            }

            _logger.LogInformation("Error Queue Metrics Service stopped.");
        }

        /// <summary>
        /// Collects metrics from RabbitMQ error queues.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task CollectMetrics(CancellationToken cancellationToken)
        {
            var queues = await _rabbitClient.GetQueuesAsync(cancellationToken);
            var errorQueues = queues.Where(q =>
                q.Name.EndsWith("_error") || q.Name.EndsWith("_skipped"));

            foreach (var queue in errorQueues)
            {
                try
                {
                    var originalQueue = DeriveOriginalQueueName(queue.Name);

                    // Update gauges
                    ErrorQueueDepth.WithLabels(queue.Name, originalQueue)
                        .Set(queue.Messages);

                    ErrorQueueBytes.WithLabels(queue.Name)
                        .Set(queue.MessageBytes);

                    // Calculate oldest message age if queue has messages
                    if (queue.Messages > 0)
                    {
                        var oldestMessageAge = await GetOldestMessageAge(queue.Name, cancellationToken);
                        if (oldestMessageAge.HasValue)
                        {
                            ErrorQueueOldestMessageAge.WithLabels(queue.Name)
                                .Set(oldestMessageAge.Value.TotalSeconds);
                        }
                    }
                    else
                    {
                        // Reset age to 0 if queue is empty
                        ErrorQueueOldestMessageAge.WithLabels(queue.Name).Set(0);
                    }

                    // Sample message sizes (get a few messages to calculate size distribution)
                    if (queue.Messages > 0)
                    {
                        await SampleMessageSizes(queue.Name, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error collecting metrics for queue {QueueName}", queue.Name);
                }
            }

            _logger.LogDebug("Collected metrics for {QueueCount} error queues", errorQueues.Count());
        }

        /// <summary>
        /// Derives the original queue name from an error queue name.
        /// </summary>
        /// <param name="errorQueueName">Error queue name.</param>
        /// <returns>Original queue name.</returns>
        private string DeriveOriginalQueueName(string errorQueueName)
        {
            if (errorQueueName.EndsWith("_error"))
            {
                return errorQueueName.Substring(0, errorQueueName.Length - "_error".Length);
            }
            else if (errorQueueName.EndsWith("_skipped"))
            {
                return errorQueueName.Substring(0, errorQueueName.Length - "_skipped".Length);
            }

            return errorQueueName;
        }

        /// <summary>
        /// Gets the age of the oldest message in a queue.
        /// </summary>
        /// <param name="queueName">Queue name.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Age of oldest message, or null if unable to determine.</returns>
        private async Task<TimeSpan?> GetOldestMessageAge(string queueName, CancellationToken cancellationToken)
        {
            try
            {
                // Get a sample of messages to find the oldest
                var messages = await _rabbitClient.GetMessagesAsync(queueName, 10, cancellationToken);
                if (!messages.Any())
                {
                    return null;
                }

                // Find the oldest timestamp
                var oldestTimestamp = messages
                    .Where(m => m.Properties?.Timestamp.HasValue == true)
                    .Select(m => m.Properties.Timestamp!.Value)
                    .DefaultIfEmpty(0)
                    .Min();

                if (oldestTimestamp == 0)
                {
                    return null;
                }

                // Convert Unix timestamp to DateTime and calculate age
                var oldestDate = DateTimeOffset.FromUnixTimeSeconds(oldestTimestamp).UtcDateTime;
                return DateTime.UtcNow - oldestDate;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to determine oldest message age for queue {QueueName}", queueName);
                return null;
            }
        }

        /// <summary>
        /// Samples message sizes for histogram metrics.
        /// </summary>
        /// <param name="queueName">Queue name.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task SampleMessageSizes(string queueName, CancellationToken cancellationToken)
        {
            try
            {
                // Get a sample of messages
                var messages = await _rabbitClient.GetMessagesAsync(queueName, 5, cancellationToken);

                foreach (var message in messages)
                {
                    var messageType = message.Properties?.Type ?? "unknown";
                    var messageSize = message.PayloadBytes;

                    ErrorQueueMessageSize
                        .WithLabels(queueName, messageType)
                        .Observe(messageSize);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to sample message sizes for queue {QueueName}", queueName);
            }
        }

        /// <summary>
        /// Records an error queue operation duration.
        /// </summary>
        /// <param name="operation">Operation name.</param>
        /// <param name="queueName">Queue name.</param>
        /// <returns>Timer that records duration when disposed.</returns>
        public static Prometheus.ITimer StartOperationTimer(string operation, string queueName)
        {
            return ErrorQueueOperationDuration
                .WithLabels(operation, queueName)
                .NewTimer();
        }

        /// <summary>
        /// Increments the replay counter.
        /// </summary>
        /// <param name="queueName">Queue name.</param>
        /// <param name="status">Replay status (success/failed).</param>
        /// <param name="count">Number of messages.</param>
        public static void RecordReplay(string queueName, string status, int count = 1)
        {
            ErrorQueueMessagesReplayedTotal
                .WithLabels(queueName, status)
                .Inc(count);
        }

        /// <summary>
        /// Increments the delete counter.
        /// </summary>
        /// <param name="queueName">Queue name.</param>
        /// <param name="reason">Deletion reason.</param>
        /// <param name="count">Number of messages.</param>
        public static void RecordDeletion(string queueName, string reason, int count = 1)
        {
            ErrorQueueMessagesDeletedTotal
                .WithLabels(queueName, reason)
                .Inc(count);
        }

        /// <summary>
        /// Increments the error queue message counter.
        /// </summary>
        /// <param name="queueName">Queue name.</param>
        /// <param name="originalQueue">Original queue name.</param>
        /// <param name="messageType">Message type.</param>
        public static void RecordErrorMessage(string queueName, string originalQueue, string messageType)
        {
            ErrorQueueMessagesTotal
                .WithLabels(queueName, originalQueue, messageType)
                .Inc();
        }
    }
}