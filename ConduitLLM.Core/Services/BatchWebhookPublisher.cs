using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Events;

using MassTransit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Configuration for batch webhook publishing.
    /// </summary>
    public class BatchWebhookPublisherOptions
    {
        /// <summary>
        /// Maximum number of webhooks to batch together.
        /// </summary>
        public int MaxBatchSize { get; set; } = 100;

        /// <summary>
        /// Maximum time to wait before sending a partial batch.
        /// </summary>
        public TimeSpan MaxBatchDelay { get; set; } = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// Number of concurrent batch publishers.
        /// </summary>
        public int ConcurrentPublishers { get; set; } = 3;
    }

    /// <summary>
    /// Service that batches webhook delivery requests for optimal throughput.
    /// Reduces network overhead and improves performance for high-volume webhook scenarios.
    /// </summary>
    public class BatchWebhookPublisher : BackgroundService
    {
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IOptions<BatchWebhookPublisherOptions> _options;
        private readonly ILogger<BatchWebhookPublisher> _logger;
        
        private readonly ConcurrentQueue<WebhookDeliveryRequested> _queue = new();
        private readonly SemaphoreSlim _batchSemaphore;
        private readonly Timer _batchTimer;
        
        private long _totalPublished = 0;
        private long _totalBatches = 0;

        public BatchWebhookPublisher(
            IPublishEndpoint publishEndpoint,
            IOptions<BatchWebhookPublisherOptions> options,
            ILogger<BatchWebhookPublisher> logger)
        {
            _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _batchSemaphore = new SemaphoreSlim(1, 1);
            _batchTimer = new Timer(async _ => await PublishBatchAsync(), null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Enqueue a webhook for batch delivery.
        /// </summary>
        public void EnqueueWebhook(WebhookDeliveryRequested webhook)
        {
            if (webhook == null) throw new ArgumentNullException(nameof(webhook));
            
            _queue.Enqueue(webhook);
            
            // If we've reached the batch size, trigger immediate publishing
            if (_queue.Count >= _options.Value.MaxBatchSize)
            {
                _ = Task.Run(async () => await PublishBatchAsync());
            }
            else
            {
                // Reset the timer to ensure we don't wait too long
                _batchTimer.Change(_options.Value.MaxBatchDelay, Timeout.InfiniteTimeSpan);
            }
        }

        /// <summary>
        /// Bulk enqueue multiple webhooks.
        /// </summary>
        public void EnqueueWebhooks(IEnumerable<WebhookDeliveryRequested> webhooks)
        {
            foreach (var webhook in webhooks)
            {
                _queue.Enqueue(webhook);
            }
            
            _ = Task.Run(async () => await PublishBatchAsync());
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "Batch webhook publisher started with MaxBatchSize={MaxBatchSize}, MaxBatchDelay={MaxBatchDelay}ms",
                _options.Value.MaxBatchSize,
                _options.Value.MaxBatchDelay.TotalMilliseconds);

            // Start the batch timer
            _batchTimer.Change(_options.Value.MaxBatchDelay, _options.Value.MaxBatchDelay);

            // Run concurrent publishers
            var publisherTasks = new Task[_options.Value.ConcurrentPublishers];
            for (int i = 0; i < _options.Value.ConcurrentPublishers; i++)
            {
                publisherTasks[i] = RunPublisherAsync(i, stoppingToken);
            }

            await Task.WhenAll(publisherTasks);

            // Publish any remaining webhooks
            await PublishBatchAsync();

            _logger.LogInformation(
                "Batch webhook publisher stopped. Total published: {TotalPublished} in {TotalBatches} batches",
                _totalPublished,
                _totalBatches);
        }

        private async Task RunPublisherAsync(int publisherId, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Publisher {PublisherId} started", publisherId);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Wait for signal or timeout
                    await Task.Delay(_options.Value.MaxBatchDelay, cancellationToken);
                    
                    // Publish any pending webhooks
                    await PublishBatchAsync();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in publisher {PublisherId}", publisherId);
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                }
            }

            _logger.LogDebug("Publisher {PublisherId} stopped", publisherId);
        }

        private async Task PublishBatchAsync()
        {
            if (!await _batchSemaphore.WaitAsync(0))
            {
                // Another thread is already publishing
                return;
            }

            try
            {
                var batch = new List<WebhookDeliveryRequested>(_options.Value.MaxBatchSize);
                
                // Dequeue up to MaxBatchSize items
                while (batch.Count < _options.Value.MaxBatchSize && _queue.TryDequeue(out var webhook))
                {
                    batch.Add(webhook);
                }

                if (batch.Count == 0)
                {
                    return;
                }

                // Group by partition key for ordered processing
                var groupedBatches = batch.GroupBy(w => w.PartitionKey);

                foreach (var group in groupedBatches)
                {
                    var webhooks = group.ToList();
                    
                    try
                    {
                        // Use PublishBatch for efficiency
                        await _publishEndpoint.PublishBatch(webhooks);
                        
                        Interlocked.Add(ref _totalPublished, webhooks.Count);
                        Interlocked.Increment(ref _totalBatches);

                        _logger.LogDebug(
                            "Published batch of {Count} webhooks for partition {PartitionKey}",
                            webhooks.Count,
                            group.Key);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Failed to publish batch of {Count} webhooks for partition {PartitionKey}",
                            webhooks.Count,
                            group.Key);

                        // Re-queue failed webhooks
                        foreach (var webhook in webhooks)
                        {
                            _queue.Enqueue(webhook);
                        }
                    }
                }

                // Log performance metrics periodically
                if (_totalBatches % 100 == 0)
                {
                    _logger.LogInformation(
                        "Webhook batch publisher metrics: TotalPublished={TotalPublished}, TotalBatches={TotalBatches}, " +
                        "AvgBatchSize={AvgBatchSize:F2}, QueueDepth={QueueDepth}",
                        _totalPublished,
                        _totalBatches,
                        (double)_totalPublished / _totalBatches,
                        _queue.Count);
                }
            }
            finally
            {
                _batchSemaphore.Release();
            }
        }

        public override void Dispose()
        {
            _batchTimer?.Dispose();
            _batchSemaphore?.Dispose();
            base.Dispose();
        }
    }

    /// <summary>
    /// Extension methods for registering batch webhook publisher.
    /// </summary>
    public static class BatchWebhookPublisherExtensions
    {
        /// <summary>
        /// Adds the batch webhook publisher service.
        /// </summary>
        public static IServiceCollection AddBatchWebhookPublisher(
            this IServiceCollection services,
            Action<BatchWebhookPublisherOptions>? configure = null)
        {
            if (configure != null)
            {
                services.Configure(configure);
            }
            else
            {
                services.Configure<BatchWebhookPublisherOptions>(options =>
                {
                    // Production-optimized defaults
                    options.MaxBatchSize = 100;
                    options.MaxBatchDelay = TimeSpan.FromMilliseconds(100);
                    options.ConcurrentPublishers = 3;
                });
            }

            services.AddSingleton<BatchWebhookPublisher>();
            services.AddHostedService(provider => provider.GetRequiredService<BatchWebhookPublisher>());

            return services;
        }
    }
}