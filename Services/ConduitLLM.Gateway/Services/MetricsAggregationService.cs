using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using ConduitLLM.Configuration.DTOs.Metrics;
using ConduitLLM.Gateway.Hubs;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Gateway.Metrics;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Service that aggregates metrics from various sources and provides them to the dashboard.
    /// </summary>
    public partial class MetricsAggregationService : BackgroundService, IMetricsAggregationService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MetricsAggregationService> _logger;
        private readonly IHubContext<MetricsHub> _hubContext;
        private readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(5);
        private readonly Dictionary<string, MetricsSeries> _historicalData = new();
        private readonly object _dataLock = new();
        private MetricsSnapshot? _lastSnapshot;

        // Alert thresholds
        private const double ErrorRateThreshold = 5.0; // 5% error rate
        private const double ResponseTimeThreshold = 5000; // 5 seconds
        private const double CpuUsageThreshold = 80.0; // 80% CPU
        private const double MemoryUsageThreshold = 85.0; // 85% memory
        private const int QueueDepthThreshold = 1000; // 1000 messages

        public MetricsAggregationService(
            IServiceProvider serviceProvider,
            ILogger<MetricsAggregationService> logger,
            IHubContext<MetricsHub> hubContext)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _hubContext = hubContext;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Metrics aggregation service starting with {IntervalSeconds}s collection interval",
                _updateInterval.TotalSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                    var snapshot = await CollectMetricsSnapshotAsync();
                    _lastSnapshot = snapshot;

                    // Store historical data
                    StoreHistoricalData(snapshot);

                    // Broadcast to all subscribers
                    await _hubContext.Clients.Group("metrics-subscribers")
                        .SendAsync("MetricsSnapshot", snapshot, stoppingToken);

                    // Send targeted updates
                    await SendTargetedUpdates(snapshot, stoppingToken);

                    // Check for alerts
                    await CheckAndSendAlerts(snapshot, stoppingToken);

                    stopwatch.Stop();
                    _logger.LogDebug(
                        "Metrics aggregation cycle completed in {ElapsedMs}ms — " +
                        "HTTP requests/s: {RequestsPerSec:F1}, error rate: {ErrorRate:F1}%, " +
                        "active requests: {ActiveRequests}, CPU: {Cpu:F1}%, memory: {MemoryMB:F0}MB",
                        stopwatch.ElapsedMilliseconds,
                        snapshot.Http.RequestsPerSecond,
                        snapshot.Http.ErrorRate,
                        snapshot.Http.ActiveRequests,
                        snapshot.System.CpuUsagePercent,
                        snapshot.System.MemoryUsageMB);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in metrics aggregation cycle");
                }

                await Task.Delay(_updateInterval, stoppingToken);
            }

            _logger.LogInformation("Metrics aggregation service stopped");
        }

        public async Task<MetricsSnapshot> GetCurrentSnapshotAsync()
        {
            if (_lastSnapshot != null && (DateTime.UtcNow - _lastSnapshot.Timestamp).TotalSeconds < 10)
            {
                return _lastSnapshot;
            }

            return await CollectMetricsSnapshotAsync();
        }

        private async Task<MetricsSnapshot> CollectMetricsSnapshotAsync()
        {
            using var collectionTimer = MetricsCollectionInstrumentation.Measure("metrics_aggregation");
            var snapshot = new MetricsSnapshot
            {
                Timestamp = DateTime.UtcNow
            };

            // Kick off the only I/O-bound collector so it overlaps with the cheap synchronous
            // ones below. The sync collectors are fast in-memory metric reads; wrapping them
            // in Task.Run only adds scheduling overhead.
            var businessTask = CollectBusinessMetricsAsync(snapshot);

            CollectHttpMetrics(snapshot);
            CollectInfrastructureMetrics(snapshot);
            CollectSystemMetrics(snapshot);

            await businessTask;

            return snapshot;
        }



        public async Task<HistoricalMetricsResponse> GetHistoricalMetricsAsync(HistoricalMetricsRequest request)
        {
            await Task.CompletedTask; // Make async

            lock (_dataLock)
            {
                var response = new HistoricalMetricsResponse
                {
                    StartTime = request.StartTime,
                    EndTime = request.EndTime,
                    Interval = request.Interval,
                    Series = new List<MetricsSeries>()
                };

                foreach (var metricName in request.MetricNames)
                {
                    if (_historicalData.TryGetValue(metricName, out var series))
                    {
                        var filteredSeries = new MetricsSeries
                        {
                            MetricName = series.MetricName,
                            Label = series.Label,
                            DataPoints = [
                                ..series.DataPoints
                                    .Where(p => p.Timestamp >= request.StartTime && p.Timestamp <= request.EndTime)
                            ]
                        };

                        if (filteredSeries.DataPoints.Any())
                        {
                            response.Series.Add(filteredSeries);
                        }
                    }
                }

                return response;
            }
        }

        public async Task<List<MetricAlert>> GetActiveAlertsAsync()
        {
            var snapshot = await GetCurrentSnapshotAsync();
            var alerts = new List<MetricAlert>();

            // Check various thresholds and generate alerts
            if (snapshot.Http.ErrorRate > ErrorRateThreshold)
            {
                alerts.Add(new MetricAlert
                {
                    Id = "http-error-rate",
                    Severity = "critical",
                    MetricName = "HTTP Error Rate",
                    Message = $"HTTP error rate exceeds threshold",
                    CurrentValue = snapshot.Http.ErrorRate,
                    Threshold = ErrorRateThreshold,
                    TriggeredAt = DateTime.UtcNow,
                    IsActive = true
                });
            }

            return alerts;
        }

        public async Task<List<VirtualKeyStats>> GetTopVirtualKeysAsync(string metric, int count)
        {
            var snapshot = await GetCurrentSnapshotAsync();
            
            return metric.ToLower() switch
            {
                "requests" => [
                    ..snapshot.Business.TopVirtualKeys
                        .OrderByDescending(k => k.RequestsPerMinute)
                        .Take(count)
                ],
                "spend" => [
                    ..snapshot.Business.TopVirtualKeys
                        .OrderByDescending(k => k.TotalSpend)
                        .Take(count)
                ],
                "budget" => [
                    ..snapshot.Business.TopVirtualKeys
                        .OrderByDescending(k => k.BudgetUtilization)
                        .Take(count)
                ],
                _ => [..snapshot.Business.TopVirtualKeys.Take(count)]
            };
        }
    }
}
