using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Health check service for monitoring distributed cache statistics accuracy and performance.
    /// </summary>
    public partial class CacheStatisticsHealthCheck : BackgroundService, IStatisticsHealthCheck
    {
        private readonly IDistributedCacheStatisticsCollector _statisticsCollector;
        private readonly IConnectionMultiplexer? _redis;
        private readonly ILogger<CacheStatisticsHealthCheck> _logger;
        private readonly ConcurrentDictionary<string, StatisticsMonitoringAlert> _activeAlerts;
        private readonly ConcurrentDictionary<string, DateTime> _lastInstanceHeartbeat;
        private readonly ConcurrentDictionary<string, PerformanceTracker> _performanceTrackers;
        private StatisticsAlertThresholds _alertThresholds;
        private readonly SemaphoreSlim _checkLock = new(1, 1);
        private DateTime _lastHealthCheck = DateTime.UtcNow;
        private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30);

        public event EventHandler<StatisticsMonitoringAlertEventArgs>? AlertTriggered;

        public CacheStatisticsHealthCheck(
            ICacheStatisticsCollector statisticsCollector,
            IConnectionMultiplexer? redis,
            ILogger<CacheStatisticsHealthCheck> logger,
            IOptions<StatisticsAlertThresholds>? alertThresholds = null)
        {
            _statisticsCollector = statisticsCollector as IDistributedCacheStatisticsCollector 
                ?? throw new ArgumentException("Statistics collector must be distributed for health monitoring", nameof(statisticsCollector));
            _redis = redis;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _alertThresholds = alertThresholds?.Value ?? new StatisticsAlertThresholds();
            
            _activeAlerts = new ConcurrentDictionary<string, StatisticsMonitoringAlert>();
            _lastInstanceHeartbeat = new ConcurrentDictionary<string, DateTime>();
            _performanceTrackers = new ConcurrentDictionary<string, PerformanceTracker>();

            // Subscribe to distributed statistics events
            if (_statisticsCollector is IDistributedCacheStatisticsCollector distributed)
            {
                distributed.DistributedStatisticsUpdated += OnDistributedStatisticsUpdated;
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Cache statistics health monitoring started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _checkLock.WaitAsync(stoppingToken);
                    try
                    {
                        // Perform health checks
                        var healthResult = await PerformHealthCheckAsync(stoppingToken);
                        await ProcessHealthResult(healthResult);

                        // Perform accuracy validation
                        var accuracyReport = await PerformAccuracyValidationAsync(stoppingToken);
                        await ProcessAccuracyReport(accuracyReport);

                        // Update performance metrics
                        await UpdatePerformanceMetricsAsync(stoppingToken);

                        _lastHealthCheck = DateTime.UtcNow;
                    }
                    finally
                    {
                        _checkLock.Release();
                    }

                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // Expected when cancellation is requested
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in statistics health monitoring");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Back off on error
                }
            }

            _logger.LogInformation("Cache statistics health monitoring stopped");
        }

        public async Task<StatisticsHealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            return await PerformHealthCheckAsync(cancellationToken);
        }

        private async Task<StatisticsHealthCheckResult> PerformHealthCheckAsync(CancellationToken cancellationToken)
        {
            var result = new StatisticsHealthCheckResult
            {
                Status = HealthStatus.Healthy,
                ComponentHealth = new Dictionary<string, ComponentHealth>()
            };

            try
            {
                // Check Redis connectivity
                if (_redis != null)
                {
                    var redisHealth = await CheckRedisHealthAsync();
                    result.RedisConnected = redisHealth.Status == HealthStatus.Healthy;
                    result.ComponentHealth["Redis"] = redisHealth;
                    
                    if (!result.RedisConnected)
                    {
                        result.Status = HealthStatus.Unhealthy;
                        result.Messages.Add("Redis connection is unhealthy");
                    }
                }
                else
                {
                    result.RedisConnected = false;
                    result.ComponentHealth["Redis"] = new ComponentHealth
                    {
                        Name = "Redis",
                        Status = HealthStatus.Unhealthy,
                        Message = "Redis not configured",
                        LastCheck = DateTime.UtcNow
                    };
                }

                // Check active instances
                var activeInstances = await _statisticsCollector.GetActiveInstancesAsync(cancellationToken);
                result.ActiveInstances = activeInstances.Count();
                
                // Update heartbeats
                foreach (var instance in activeInstances)
                {
                    _lastInstanceHeartbeat[instance] = DateTime.UtcNow;
                }

                // Check for missing instances
                var now = DateTime.UtcNow;
                var missingInstances = _lastInstanceHeartbeat
                    .Where(kvp => now - kvp.Value > _alertThresholds.MaxInstanceMissingTime)
                    .Select(kvp => kvp.Key)
                    .ToList();

                result.MissingInstances = missingInstances.Count();
                
                if (missingInstances.Count() > 0)
                {
                    result.Status = HealthStatus.Degraded;
                    result.Messages.Add($"{missingInstances.Count()} instances not reporting");
                }

                // Check aggregation performance
                var aggregationHealth = await CheckAggregationPerformanceAsync(cancellationToken);
                result.ComponentHealth["Aggregation"] = aggregationHealth;
                result.AggregationLatencyMs = aggregationHealth.Status == HealthStatus.Healthy 
                    ? GetLatestAggregationLatency() 
                    : -1;

                if (aggregationHealth.Status == HealthStatus.Unhealthy)
                {
                    result.Status = HealthStatus.Degraded;
                }

                // Check Redis memory usage
                if (_redis != null)
                {
                    result.RedisMemoryUsageBytes = await GetRedisMemoryUsageAsync();
                    
                    if (result.RedisMemoryUsageBytes > _alertThresholds.MaxRedisMemoryBytes)
                    {
                        result.Status = HealthStatus.Degraded;
                        result.Messages.Add($"Redis memory usage ({result.RedisMemoryUsageBytes / (1024 * 1024)}MB) exceeds threshold");
                    }
                }

                result.LastSuccessfulAggregation = GetLastSuccessfulAggregation();

                // Check minimum instances
                if (result.ActiveInstances < _alertThresholds.MinActiveInstances)
                {
                    result.Status = HealthStatus.Degraded;
                    result.Messages.Add($"Active instances ({result.ActiveInstances}) below minimum threshold ({_alertThresholds.MinActiveInstances})");
                }

                // Process the health check result to trigger any necessary alerts
                await ProcessHealthResult(result);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing health check");
                result.Status = HealthStatus.Unhealthy;
                result.Messages.Add($"Health check error: {ex.Message}");
                return result;
            }
        }

        public async Task<StatisticsAccuracyReport> ValidateAccuracyAsync(CancellationToken cancellationToken = default)
        {
            return await PerformAccuracyValidationAsync(cancellationToken);
        }

        public async Task<StatisticsPerformanceMetrics> GetPerformanceMetricsAsync(CancellationToken cancellationToken = default)
        {
            // Implementation in Performance.cs partial class
            return await GetPerformanceMetricsAsyncImpl(cancellationToken);
        }

        public Task ConfigureAlertingAsync(StatisticsAlertThresholds thresholds)
        {
            // Implementation in Performance.cs partial class
            return ConfigureAlertingAsyncImpl(thresholds);
        }

        public Task<IEnumerable<StatisticsMonitoringAlert>> GetActiveAlertsAsync(CancellationToken cancellationToken = default)
        {
            // Implementation in Performance.cs partial class
            return GetActiveAlertsAsyncImpl(cancellationToken);
        }

        public override void Dispose()
        {
            _checkLock?.Dispose();
            base.Dispose();
        }
    }
}
