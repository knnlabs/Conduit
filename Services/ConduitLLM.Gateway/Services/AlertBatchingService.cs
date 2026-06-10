using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using ConduitLLM.Configuration.DTOs.HealthMonitoring;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Background service that batches alerts for efficient notification delivery.
    /// Uses a Channel-based work queue for proper error handling and graceful shutdown.
    /// </summary>
    public class AlertBatchingService : BackgroundService
    {
        private readonly IAlertNotificationService _notificationService;
        private readonly ILogger<AlertBatchingService> _logger;
        private readonly AlertNotificationOptions _options;
        private readonly ConcurrentQueue<HealthAlert> _alertQueue;
        private readonly SemaphoreSlim _batchSemaphore;
        private readonly Channel<AlertWorkItem> _workChannel;

        // Work item types for channel-based processing
        private abstract record AlertWorkItem;
        private record SendImmediateAlert(HealthAlert Alert) : AlertWorkItem;
        private record QueueForBatch(HealthAlert Alert) : AlertWorkItem;
        private record ProcessBatchNow : AlertWorkItem;
        private record TimerTick : AlertWorkItem;

        public AlertBatchingService(
            IAlertNotificationService notificationService,
            ILogger<AlertBatchingService> logger,
            IOptions<AlertNotificationOptions> options)
        {
            _notificationService = notificationService;
            _logger = logger;
            _options = options.Value;
            _alertQueue = new ConcurrentQueue<HealthAlert>();
            _batchSemaphore = new SemaphoreSlim(1, 1);

            // Bounded so a stalled processor cannot grow memory without limit; the oldest
            // (least relevant) work items are dropped first and the drop is logged.
            _workChannel = Channel.CreateBounded<AlertWorkItem>(
                new BoundedChannelOptions(5000)
                {
                    SingleReader = true,
                    SingleWriter = false,
                    FullMode = BoundedChannelFullMode.DropOldest
                },
                dropped => _logger.LogWarning("Alert work queue full; dropped {WorkItemType}", dropped.GetType().Name));
        }

        /// <summary>
        /// Queue an alert for batched delivery
        /// </summary>
        public void QueueAlert(HealthAlert alert)
        {
            if (!_options.EnableBatching)
            {
                // Signal to send immediately via the work channel
                if (!_workChannel.Writer.TryWrite(new SendImmediateAlert(alert)))
                {
                    _logger.LogWarning("Failed to queue immediate alert - channel may be closed");
                }
                return;
            }

            // Signal to queue for batch
            if (!_workChannel.Writer.TryWrite(new QueueForBatch(alert)))
            {
                _logger.LogWarning("Failed to queue alert for batching - channel may be closed");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "Alert batching service started — batching {Enabled}, interval: {IntervalSeconds}s, max batch size: {MaxBatchSize}",
                _options.EnableBatching ? "enabled" : "disabled",
                _options.BatchIntervalSeconds,
                _options.MaxBatchSize);

            // Start the batch timer task
            var timerTask = RunBatchTimerAsync(stoppingToken);

            // Process work items from the channel
            try
            {
                await foreach (var workItem in _workChannel.Reader.ReadAllAsync(stoppingToken))
                {
                    try
                    {
                        await ProcessWorkItemAsync(workItem);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing alert work item of type {WorkItemType}", workItem.GetType().Name);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
                _logger.LogInformation("Alert batching service stopping - processing remaining items");
            }

            // Wait for timer to stop
            try
            {
                await timerTask;
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
        }

        private async Task RunBatchTimerAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.BatchIntervalSeconds));

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    _workChannel.Writer.TryWrite(new TimerTick());
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
            }
        }

        private async Task ProcessWorkItemAsync(AlertWorkItem workItem)
        {
            switch (workItem)
            {
                case SendImmediateAlert immediate:
                    await SendImmediateAlertAsync(immediate.Alert);
                    break;

                case QueueForBatch queue:
                    _alertQueue.Enqueue(queue.Alert);
                    // Check if batch size threshold exceeded
                    if (_alertQueue.Count >= _options.MaxBatchSize)
                    {
                        await ProcessBatchAsync();
                    }
                    break;

                case ProcessBatchNow:
                case TimerTick:
                    await ProcessBatchAsync();
                    break;
            }
        }

        private async Task SendImmediateAlertAsync(HealthAlert alert)
        {
            try
            {
                _logger.LogDebug("Sending immediate alert: {AlertType} [{Severity}] for {Component}",
                    alert.Type, alert.Severity, alert.Component);
                await _notificationService.SendAlertAsync(alert);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send immediate alert: {AlertType} [{Severity}] for {Component}",
                    alert.Type, alert.Severity, alert.Component);
            }
        }

        private async Task ProcessBatchAsync()
        {
            if (!await _batchSemaphore.WaitAsync(0))
            {
                // Another batch is already being processed
                return;
            }

            try
            {
                var alerts = new List<HealthAlert>();
                
                // Dequeue all alerts up to max batch size
                while (alerts.Count() < _options.MaxBatchSize && _alertQueue.TryDequeue(out var alert))
                {
                    alerts.Add(alert);
                }

                if (alerts.Any())
                {
                    _logger.LogInformation("Processing batch of {Count} alerts (queue remaining: {QueueRemaining})",
                        alerts.Count, _alertQueue.Count);

                    try
                    {
                        await _notificationService.SendBatchAlertsAsync(alerts);
                        _logger.LogDebug("Successfully delivered batch of {Count} alerts", alerts.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send alert batch of {Count} alerts — re-queuing", alerts.Count);

                        // Re-queue failed alerts
                        foreach (var alert in alerts)
                        {
                            _alertQueue.Enqueue(alert);
                        }
                    }
                }
            }
            finally
            {
                _batchSemaphore.Release();
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Alert batching service stopping");

            // Complete the channel to stop accepting new items
            _workChannel.Writer.Complete();

            // Process any remaining alerts in the queue
            await ProcessBatchAsync();

            await base.StopAsync(cancellationToken);
        }

        public override void Dispose()
        {
            _batchSemaphore?.Dispose();
            base.Dispose();
        }
    }
}