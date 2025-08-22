using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using ConduitLLM.Configuration.DTOs.HealthMonitoring;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Background service that batches alerts for efficient notification delivery
    /// </summary>
    public class AlertBatchingService : BackgroundService
    {
        private readonly IAlertNotificationService _notificationService;
        private readonly ILogger<AlertBatchingService> _logger;
        private readonly AlertNotificationOptions _options;
        private readonly ConcurrentQueue<HealthAlert> _alertQueue;
        private readonly SemaphoreSlim _batchSemaphore;
        private Timer? _batchTimer;

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
        }

        /// <summary>
        /// Queue an alert for batched delivery
        /// </summary>
        public void QueueAlert(HealthAlert alert)
        {
            if (!_options.EnableBatching)
            {
                // If batching is disabled, send immediately
                _ = Task.Run(async () => await _notificationService.SendAlertAsync(alert));
                return;
            }

            _alertQueue.Enqueue(alert);
            
            // If queue is getting large, trigger immediate batch
            if (_alertQueue.Count() >= _options.MaxBatchSize)
            {
                _ = Task.Run(async () => await ProcessBatchAsync());
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Alert batching service started");

            // Set up batch timer
            _batchTimer = new Timer(
                async _ => await ProcessBatchAsync(),
                null,
                TimeSpan.FromSeconds(_options.BatchIntervalSeconds),
                TimeSpan.FromSeconds(_options.BatchIntervalSeconds));

            // Keep service running
            await Task.Delay(Timeout.Infinite, stoppingToken);
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

                if (alerts.Count() > 0)
                {
                    _logger.LogInformation("Processing batch of {Count} alerts", alerts.Count());
                    
                    try
                    {
                        await _notificationService.SendBatchAlertsAsync(alerts);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send alert batch");
                        
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

            // Stop the timer
            _batchTimer?.Dispose();

            // Process any remaining alerts
            await ProcessBatchAsync();

            await base.StopAsync(cancellationToken);
        }

        public override void Dispose()
        {
            _batchTimer?.Dispose();
            _batchSemaphore?.Dispose();
            base.Dispose();
        }
    }
}