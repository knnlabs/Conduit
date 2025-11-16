using ConduitLLM.Http.Metrics;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Background service that collects and reports SignalR metrics using OpenTelemetry
    /// </summary>
    public class SignalROpenTelemetryService : BackgroundService
    {
        private readonly ILogger<SignalROpenTelemetryService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly SignalRMetrics _metrics;
        private Timer? _metricsTimer;

        public SignalROpenTelemetryService(
            ILogger<SignalROpenTelemetryService> logger,
            IServiceProvider serviceProvider,
            SignalRMetrics metrics)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _metrics = metrics;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SignalR Metrics Service starting");

            // Start periodic metrics collection
            _metricsTimer = new Timer(
                CollectMetrics,
                null,
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(30));

            // Wait until cancellation is requested
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private void CollectMetrics(object? state)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                
                // Collect connection metrics
                var connectionMonitor = scope.ServiceProvider.GetService<ISignalRConnectionMonitor>();
                if (connectionMonitor != null)
                {
                    var stats = connectionMonitor.GetStatistics();
                    
                    // Update gauge metrics
                    foreach (var hub in stats.ConnectionsByHub)
                    {
                        _metrics.UpdateActiveConnections(hub.Key, 0); // Reset to current value
                    }
                    
                    // Record acknowledgment rate
                    if (stats.TotalMessagesSent > 0)
                    {
                        var ackRate = (double)stats.TotalMessagesAcknowledged / stats.TotalMessagesSent * 100;
                        _logger.LogDebug("Acknowledgment rate: {Rate}%", ackRate);
                    }
                }

                // Collect queue metrics
                var queueService = scope.ServiceProvider.GetService<ISignalRMessageQueueService>();
                if (queueService != null)
                {
                    var stats = queueService.GetStatistics();
                    _metrics.UpdateQueueDepth(stats.PendingMessages);
                    _metrics.UpdateDeadLetterQueueDepth(stats.DeadLetterMessages);
                }

                // Collect batching metrics
                var batchingService = scope.ServiceProvider.GetService<ISignalRMessageBatcher>();
                if (batchingService != null)
                {
                    var stats = batchingService.GetStatistics();
                    _metrics.UpdatePendingBatches((int)stats.CurrentPendingMessages);
                    
                    if (stats.BatchEfficiencyPercentage > 0)
                    {
                        _logger.LogDebug("Batch efficiency: {Efficiency}%", stats.BatchEfficiencyPercentage);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting SignalR metrics");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SignalR Metrics Service stopping");
            
            _metricsTimer?.Change(Timeout.Infinite, 0);
            _metricsTimer?.Dispose();
            
            await base.StopAsync(cancellationToken);
        }

        public override void Dispose()
        {
            _metricsTimer?.Dispose();
            base.Dispose();
        }
    }

    /// <summary>
    /// Extension methods for registering SignalR metrics
    /// </summary>
    public static class SignalRMetricsExtensions
    {
        /// <summary>
        /// Adds SignalR metrics to the service collection
        /// </summary>
        public static IServiceCollection AddSignalRMetrics(this IServiceCollection services)
        {
            services.AddSingleton<SignalRMetrics>();
            services.AddHostedService<SignalROpenTelemetryService>();
            
            return services;
        }

        /// <summary>
        /// Records a SignalR operation with metrics
        /// </summary>
        public static async Task<T> RecordSignalROperationAsync<T>(
            this SignalRMetrics metrics,
            string hub,
            string method,
            Func<Task<T>> operation)
        {
            using var activity = SignalRMetrics.StartMessageActivity($"SignalR.{method}", hub, method);
            var startTime = DateTime.UtcNow;
            
            try
            {
                var result = await operation();
                
                var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
                metrics.RecordMessageDeliveryDuration(hub, method, duration);
                metrics.RecordMessageDelivered(hub, method, true);
                
                return result;
            }
            catch (Exception ex)
            {
                metrics.RecordMessageDelivered(hub, method, false);
                activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }
    }
}