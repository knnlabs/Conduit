using ConduitLLM.Gateway.Metrics;

namespace ConduitLLM.Gateway.Services
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
            // Fire-and-forget async metrics collection with proper exception handling
            _ = CollectMetricsAsync();
        }

        private async Task CollectMetricsAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();

                int totalConnections = 0;
                int pendingMessages = 0;
                int deadLetterMessages = 0;

                // Collect connection metrics
                var connectionMonitor = scope.ServiceProvider.GetService<ISignalRConnectionMonitor>();
                if (connectionMonitor != null)
                {
                    var stats = await connectionMonitor.GetStatisticsAsync();

                    // Update gauge metrics
                    foreach (var hub in stats.ConnectionsByHub)
                    {
                        _metrics.UpdateActiveConnections(hub.Key, 0); // Reset to current value
                        totalConnections += hub.Value;
                    }

                    // Record acknowledgment rate
                    if (stats.TotalMessagesSent > 0)
                    {
                        var ackRate = (double)stats.TotalMessagesAcknowledged / stats.TotalMessagesSent * 100;
                        _logger.LogDebug("SignalR acknowledgment rate: {Rate:F1}%, messages sent: {Sent}, acknowledged: {Acked}",
                            ackRate, stats.TotalMessagesSent, stats.TotalMessagesAcknowledged);
                    }
                }

                // Collect queue metrics
                var queueService = scope.ServiceProvider.GetService<ISignalRMessageQueueService>();
                if (queueService != null)
                {
                    var stats = queueService.GetStatistics();
                    pendingMessages = stats.PendingMessages;
                    deadLetterMessages = stats.DeadLetterMessages;
                    _metrics.UpdateQueueDepth(pendingMessages);
                    _metrics.UpdateDeadLetterQueueDepth(deadLetterMessages);
                }

                // Collect batching metrics
                var batchingService = scope.ServiceProvider.GetService<ISignalRMessageBatcher>();
                if (batchingService != null)
                {
                    var stats = await batchingService.GetStatisticsAsync();
                    _metrics.UpdatePendingBatches((int)stats.CurrentPendingMessages);

                    if (stats.BatchEfficiencyPercentage > 0)
                    {
                        _logger.LogDebug("SignalR batch efficiency: {Efficiency:F1}%", stats.BatchEfficiencyPercentage);
                    }
                }

                _logger.LogDebug(
                    "SignalR metrics collection completed — connections: {Connections}, pending: {Pending}, dead letters: {DeadLetters}",
                    totalConnections, pendingMessages, deadLetterMessages);

                if (deadLetterMessages > 0)
                {
                    _logger.LogWarning("SignalR dead letter queue has {DeadLetterCount} messages", deadLetterMessages);
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
    }
}