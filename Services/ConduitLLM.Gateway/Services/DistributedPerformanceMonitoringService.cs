using System.Text.Json;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using ConduitLLM.Configuration.DTOs.HealthMonitoring;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Gateway.Interfaces;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Distributed performance monitoring service that stores metrics in Redis for multi-instance consistency
    /// </summary>
    public partial class DistributedPerformanceMonitoringService : IDistributedPerformanceMonitoringService, IHostedService, IDisposable
    {
        private readonly IDatabase _database;
        private readonly IDistributedAlertManagementService _alertManagementService;
        private readonly ILogger<DistributedPerformanceMonitoringService> _logger;
        private readonly PerformanceMonitoringOptions _options;

        public string InstanceId { get; }

        // Redis keys
        private const string MetricsPrefix = "perf_metrics";
        private const string EndpointMetricsPrefix = "endpoint_metrics";
        private const string CacheMetricsPrefix = "cache_metrics";
        private const string ConnectionPoolMetricsPrefix = "pool_metrics";
        private const string InstancesSetKey = "perf_monitoring_instances";
        private const string RequestsStreamKey = "request_metrics_stream";
        private const string DatabaseOpsStreamKey = "database_ops_stream";

        private Timer? _metricsAggregationTimer;
        private Timer? _thresholdCheckTimer;
        private Timer? _heartbeatTimer;
        private readonly SemaphoreSlim _aggregationSemaphore;

        public DistributedPerformanceMonitoringService(
            IConnectionMultiplexer redis,
            IDistributedAlertManagementService alertManagementService,
            ILogger<DistributedPerformanceMonitoringService> logger,
            IOptions<PerformanceMonitoringOptions> options)
        {
            _database = redis.GetDatabase();
            _alertManagementService = alertManagementService;
            _logger = logger;
            _options = options.Value;
            InstanceId = Environment.MachineName + "_" + Environment.ProcessId + "_" + Guid.NewGuid().ToString("N")[..8];
            _aggregationSemaphore = new SemaphoreSlim(1, 1);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await RegisterInstanceAsync();

            _logger.LogInformation("Distributed performance monitoring service started with instance ID: {InstanceId}", InstanceId);

            // Start heartbeat timer (every 30 seconds)
            _heartbeatTimer = new Timer(
                async _ => await UpdateHeartbeatAsync(),
                null,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(30));

            // Start metrics aggregation timer
            _metricsAggregationTimer = new Timer(
                async _ => await AggregateMetricsAsync(),
                null,
                TimeSpan.FromSeconds(_options.AggregationIntervalSeconds),
                TimeSpan.FromSeconds(_options.AggregationIntervalSeconds));

            // Start threshold checking timer
            _thresholdCheckTimer = new Timer(
                async _ => await CheckThresholdsAsync(),
                null,
                TimeSpan.FromSeconds(_options.ThresholdCheckIntervalSeconds),
                TimeSpan.FromSeconds(_options.ThresholdCheckIntervalSeconds));
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Distributed performance monitoring service stopping...");

            _heartbeatTimer?.Change(Timeout.Infinite, 0);
            _metricsAggregationTimer?.Change(Timeout.Infinite, 0);
            _thresholdCheckTimer?.Change(Timeout.Infinite, 0);

            await UnregisterInstanceAsync();
        }

        public async Task RegisterInstanceAsync()
        {
            var instanceData = new
            {
                InstanceId,
                MachineName = Environment.MachineName,
                ProcessId = Environment.ProcessId,
                StartedAt = DateTime.UtcNow,
                LastHeartbeat = DateTime.UtcNow
            };

            await _database.HashSetAsync($"{InstancesSetKey}:{InstanceId}", "data", JsonSerializer.Serialize(instanceData));
            await _database.KeyExpireAsync($"{InstancesSetKey}:{InstanceId}", TimeSpan.FromMinutes(2));
        }

        public async Task UnregisterInstanceAsync()
        {
            await _database.KeyDeleteAsync($"{InstancesSetKey}:{InstanceId}");
        }

        public async Task UpdateHeartbeatAsync()
        {
            try
            {
                await _database.HashSetAsync($"{InstancesSetKey}:{InstanceId}", "last_heartbeat", DateTime.UtcNow.Ticks);
                await _database.KeyExpireAsync($"{InstancesSetKey}:{InstanceId}", TimeSpan.FromMinutes(2));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update heartbeat for instance {InstanceId}", InstanceId);
            }
        }

        public async Task<List<string>> GetActiveInstancesAsync()
        {
            var pattern = $"{InstancesSetKey}:*";
            var server = _database.Multiplexer.GetPrimaryServer();
            var keys = server.Keys(pattern: pattern);

            var instances = new List<string>();
            var cutoffTime = DateTime.UtcNow.AddMinutes(-1); // Consider instances active if heartbeat within last minute

            foreach (var key in keys)
            {
                var lastHeartbeat = await _database.HashGetAsync(key, "last_heartbeat");
                if (lastHeartbeat.HasValue)
                {
                    var heartbeatTime = new DateTime((long)lastHeartbeat);
                    if (heartbeatTime > cutoffTime)
                    {
                        var instanceId = key.ToString().Split(':').Last();
                        instances.Add(instanceId);
                    }
                }
            }

            return instances;
        }

        public void RecordRequestMetric(string endpoint, double responseTimeMs, bool isSuccess)
        {
            // Use fire-and-forget async call to maintain interface compatibility
            _ = Task.Run(() => RecordRequestMetricAsync(endpoint, responseTimeMs, isSuccess));
        }

        public void RecordDatabaseQueryMetric(string operation, double executionTimeMs)
        {
            // Use fire-and-forget async call to maintain interface compatibility
            _ = Task.Run(() => RecordDatabaseQueryMetricAsync(operation, executionTimeMs));
        }

        public void RecordCacheMetric(string operation, bool isHit)
        {
            // Use fire-and-forget async call to maintain interface compatibility
            _ = Task.Run(() => RecordCacheMetricAsync(operation, isHit));
        }

        public void RecordConnectionPoolMetric(string poolName, int active, int idle, int waitQueue)
        {
            // Use fire-and-forget async call to maintain interface compatibility
            _ = Task.Run(() => RecordConnectionPoolMetricAsync(poolName, active, idle, waitQueue));
        }

        public async Task<PerformanceMetrics> GetCurrentMetricsAsync()
        {
            return await GetAggregatedMetricsAsync();
        }

        public async Task<Dictionary<string, EndpointMetrics>> GetEndpointMetricsAsync()
        {
            return await GetAggregatedEndpointMetricsAsync();
        }

        public void Dispose()
        {
            _heartbeatTimer?.Dispose();
            _metricsAggregationTimer?.Dispose();
            _thresholdCheckTimer?.Dispose();
            _aggregationSemaphore?.Dispose();
        }
    }
}
