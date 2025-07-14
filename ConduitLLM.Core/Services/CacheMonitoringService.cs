using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ConduitLLM.Core.Caching;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration.Models;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Monitoring-specific alert thresholds that extend the base MonitoringAlertThresholds
    /// </summary>
    public class MonitoringAlertThresholds
    {
        /// <summary>
        /// Minimum cache hit rate before triggering an alert (default: 0.5 or 50%)
        /// </summary>
        public double MinHitRate { get; set; } = 0.5;

        /// <summary>
        /// Maximum memory usage percentage before triggering an alert (default: 0.85 or 85%)
        /// </summary>
        public double MaxMemoryUsage { get; set; } = 0.85;

        /// <summary>
        /// Maximum eviction rate per minute before triggering an alert (default: 100)
        /// </summary>
        public double MaxEvictionRate { get; set; } = 100;

        /// <summary>
        /// Maximum average response time in milliseconds before triggering an alert (default: 100ms)
        /// </summary>
        public double MaxResponseTimeMs { get; set; } = 100;

        /// <summary>
        /// Minimum number of requests before evaluating hit rate (default: 100)
        /// </summary>
        public long MinRequestsForHitRateAlert { get; set; } = 100;
    }

    /// <summary>
    /// Cache alert event arguments
    /// </summary>
    public class CacheAlertEventArgs : EventArgs
    {
        public string AlertType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Severity { get; set; } = "warning";
        public string? Region { get; set; }
        public Dictionary<string, object> Details { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Service that monitors cache performance and health, triggering alerts when thresholds are exceeded
    /// </summary>
    public interface ICacheMonitoringService
    {
        /// <summary>
        /// Event raised when a cache alert is triggered
        /// </summary>
        event EventHandler<CacheAlertEventArgs>? CacheAlertTriggered;

        /// <summary>
        /// Gets the current alert thresholds
        /// </summary>
        MonitoringAlertThresholds GetThresholds();

        /// <summary>
        /// Updates the alert thresholds
        /// </summary>
        void UpdateThresholds(MonitoringAlertThresholds thresholds);

        /// <summary>
        /// Gets the current monitoring status
        /// </summary>
        Task<CacheMonitoringStatus> GetStatusAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets recent alerts
        /// </summary>
        IReadOnlyList<CacheAlertEventArgs> GetRecentAlerts(int count = 10);

        /// <summary>
        /// Clears alert history
        /// </summary>
        void ClearAlertHistory();

        /// <summary>
        /// Forces a monitoring check
        /// </summary>
        Task CheckNowAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Cache monitoring status
    /// </summary>
    public class CacheMonitoringStatus
    {
        public DateTime LastCheck { get; set; }
        public bool IsHealthy { get; set; }
        public double CurrentHitRate { get; set; }
        public double CurrentMemoryUsagePercent { get; set; }
        public double CurrentEvictionRate { get; set; }
        public double CurrentResponseTimeMs { get; set; }
        public int ActiveAlerts { get; set; }
        public Dictionary<string, object> Details { get; set; } = new();
    }

    /// <summary>
    /// Background service that monitors cache health and performance
    /// </summary>
    public class CacheMonitoringService : BackgroundService, ICacheMonitoringService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CacheMonitoringService> _logger;
        private readonly List<CacheAlertEventArgs> _alertHistory = new();
        private readonly SemaphoreSlim _alertHistoryLock = new(1, 1);
        private MonitoringAlertThresholds _thresholds = new();
        private DateTime _lastCheck = DateTime.UtcNow;
        private CacheMonitoringStatus _lastStatus = new() { IsHealthy = true };
        private readonly TimeSpan _monitoringInterval = TimeSpan.FromMinutes(1);
        private readonly int _maxAlertHistory = 100;

        // Track metrics over time for rate calculations
        private long _lastEvictionCount = 0;
        private DateTime _lastEvictionCheck = DateTime.UtcNow;

        public event EventHandler<CacheAlertEventArgs>? CacheAlertTriggered;

        public CacheMonitoringService(
            IServiceProvider serviceProvider,
            ILogger<CacheMonitoringService> logger,
            IOptions<MonitoringAlertThresholds>? thresholdOptions = null)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            if (thresholdOptions?.Value != null)
            {
                _thresholds = thresholdOptions.Value;
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Cache monitoring service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckNowAsync(stoppingToken);
                    await Task.Delay(_monitoringInterval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // Expected when cancellation is requested
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in cache monitoring service");
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Back off on error
                }
            }

            _logger.LogInformation("Cache monitoring service stopped");
        }

        public async Task CheckNowAsync(CancellationToken cancellationToken = default)
        {
            using var scope = _serviceProvider.CreateScope();
            var cacheManager = scope.ServiceProvider.GetService<ICacheManager>();
            var cacheMetrics = scope.ServiceProvider.GetService<ICacheMetricsService>();
            var cacheRegistry = scope.ServiceProvider.GetService<ICacheRegistry>();

            if (cacheManager == null || cacheMetrics == null)
            {
                _logger.LogWarning("Required cache services not available for monitoring");
                return;
            }

            var status = new CacheMonitoringStatus
            {
                LastCheck = DateTime.UtcNow,
                IsHealthy = true
            };

            var alerts = new List<CacheAlertEventArgs>();

            try
            {
                // Check cache health
                var healthStatus = await cacheManager.GetHealthStatusAsync();
                if (!healthStatus.IsHealthy)
                {
                    status.IsHealthy = false;
                    alerts.Add(new CacheAlertEventArgs
                    {
                        AlertType = "CacheUnhealthy",
                        Message = "Cache manager is reporting unhealthy status",
                        Severity = "error",
                        Details = new Dictionary<string, object>
                        {
                            ["issues"] = healthStatus.Issues,
                            ["components"] = healthStatus.ComponentStatus
                        }
                    });
                }

                // Check hit rate
                var hitRate = cacheMetrics.GetHitRate();
                var totalRequests = cacheMetrics.GetTotalRequests();
                status.CurrentHitRate = hitRate;

                if (totalRequests >= _thresholds.MinRequestsForHitRateAlert && hitRate < _thresholds.MinHitRate)
                {
                    alerts.Add(new CacheAlertEventArgs
                    {
                        AlertType = "LowHitRate",
                        Message = $"Cache hit rate is {hitRate:P1}, below threshold of {_thresholds.MinHitRate:P1}",
                        Severity = "warning",
                        Details = new Dictionary<string, object>
                        {
                            ["hitRate"] = hitRate,
                            ["threshold"] = _thresholds.MinHitRate,
                            ["totalRequests"] = totalRequests,
                            ["hits"] = cacheMetrics.GetTotalHits(),
                            ["misses"] = cacheMetrics.GetTotalMisses()
                        }
                    });
                }

                // Check response time
                var avgResponseTime = cacheMetrics.GetAverageRetrievalTimeMs();
                status.CurrentResponseTimeMs = avgResponseTime;

                if (avgResponseTime > _thresholds.MaxResponseTimeMs)
                {
                    alerts.Add(new CacheAlertEventArgs
                    {
                        AlertType = "HighResponseTime",
                        Message = $"Average cache response time is {avgResponseTime:F1}ms, above threshold of {_thresholds.MaxResponseTimeMs}ms",
                        Severity = "warning",
                        Details = new Dictionary<string, object>
                        {
                            ["avgResponseTimeMs"] = avgResponseTime,
                            ["threshold"] = _thresholds.MaxResponseTimeMs
                        }
                    });
                }

                // Check memory usage and eviction rates per region
                if (cacheRegistry != null)
                {
                    var regions = cacheRegistry.GetAllRegions();
                    double totalMemoryUsed = 0;
                    double totalMemoryLimit = 0;
                    long totalEvictions = 0;

                    foreach (var regionKvp in regions)
                    {
                        var region = regionKvp.Key;
                        var regionConfig = regionKvp.Value;
                        var stats = await cacheManager.GetRegionStatisticsAsync(region, cancellationToken);
                        if (stats != null)
                        {
                            totalMemoryUsed += stats.TotalSizeBytes;
                            totalMemoryLimit += regionConfig.MaxSizeInBytes ?? 0;
                            totalEvictions += stats.EvictionCount;

                            // Check region-specific memory usage
                            if (regionConfig.MaxSizeInBytes > 0)
                            {
                                var regionMemoryUsage = (double)stats.TotalSizeBytes / regionConfig.MaxSizeInBytes.Value;
                                if (regionMemoryUsage > _thresholds.MaxMemoryUsage)
                                {
                                    alerts.Add(new CacheAlertEventArgs
                                    {
                                        AlertType = "HighMemoryUsage",
                                        Message = $"Cache region '{region}' memory usage is {regionMemoryUsage:P1}, above threshold of {_thresholds.MaxMemoryUsage:P1}",
                                        Severity = "warning",
                                        Region = region.ToString(),
                                        Details = new Dictionary<string, object>
                                        {
                                            ["currentSizeBytes"] = stats.TotalSizeBytes,
                                            ["maxSizeBytes"] = regionConfig.MaxSizeInBytes.Value,
                                            ["usagePercent"] = regionMemoryUsage,
                                            ["threshold"] = _thresholds.MaxMemoryUsage
                                        }
                                    });
                                }
                            }
                        }
                    }

                    // Calculate overall memory usage
                    if (totalMemoryLimit > 0)
                    {
                        status.CurrentMemoryUsagePercent = totalMemoryUsed / totalMemoryLimit;
                    }

                    // Calculate eviction rate
                    var now = DateTime.UtcNow;
                    var timeSinceLastCheck = (now - _lastEvictionCheck).TotalMinutes;
                    if (timeSinceLastCheck > 0 && _lastEvictionCount > 0)
                    {
                        var evictionRate = (totalEvictions - _lastEvictionCount) / timeSinceLastCheck;
                        status.CurrentEvictionRate = evictionRate;

                        if (evictionRate > _thresholds.MaxEvictionRate)
                        {
                            alerts.Add(new CacheAlertEventArgs
                            {
                                AlertType = "HighEvictionRate",
                                Message = $"Cache eviction rate is {evictionRate:F1}/min, above threshold of {_thresholds.MaxEvictionRate}/min",
                                Severity = "warning",
                                Details = new Dictionary<string, object>
                                {
                                    ["evictionRate"] = evictionRate,
                                    ["threshold"] = _thresholds.MaxEvictionRate,
                                    ["totalEvictions"] = totalEvictions,
                                    ["periodMinutes"] = timeSinceLastCheck
                                }
                            });
                        }
                    }

                    _lastEvictionCount = totalEvictions;
                    _lastEvictionCheck = now;
                }

                // Process alerts
                status.ActiveAlerts = alerts.Count;
                status.Details = new Dictionary<string, object>
                {
                    ["healthStatus"] = healthStatus.IsHealthy ? "Healthy" : "Unhealthy",
                    ["totalRequests"] = totalRequests,
                    ["modelMetrics"] = cacheMetrics.GetModelMetrics()
                };

                foreach (var alert in alerts)
                {
                    await RaiseAlertAsync(alert);
                }

                _lastStatus = status;
                _lastCheck = DateTime.UtcNow;

                if (alerts.Count > 0)
                {
                    _logger.LogWarning("Cache monitoring detected {AlertCount} alerts", alerts.Count);
                }
                else
                {
                    _logger.LogDebug("Cache monitoring check completed, no alerts");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache monitoring check");
                status.IsHealthy = false;
                _lastStatus = status;
            }
        }

        private async Task RaiseAlertAsync(CacheAlertEventArgs alert)
        {
            try
            {
                // Add to history
                await _alertHistoryLock.WaitAsync();
                try
                {
                    _alertHistory.Add(alert);
                    
                    // Trim history if too large
                    if (_alertHistory.Count > _maxAlertHistory)
                    {
                        _alertHistory.RemoveRange(0, _alertHistory.Count - _maxAlertHistory);
                    }
                }
                finally
                {
                    _alertHistoryLock.Release();
                }

                // Raise event
                CacheAlertTriggered?.Invoke(this, alert);

                _logger.LogWarning("Cache alert triggered: {AlertType} - {Message}", alert.AlertType, alert.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising cache alert");
            }
        }

        public MonitoringAlertThresholds GetThresholds()
        {
            return new MonitoringAlertThresholds
            {
                MinHitRate = _thresholds.MinHitRate,
                MaxMemoryUsage = _thresholds.MaxMemoryUsage,
                MaxEvictionRate = _thresholds.MaxEvictionRate,
                MaxResponseTimeMs = _thresholds.MaxResponseTimeMs,
                MinRequestsForHitRateAlert = _thresholds.MinRequestsForHitRateAlert
            };
        }

        public void UpdateThresholds(MonitoringAlertThresholds thresholds)
        {
            if (thresholds == null)
                throw new ArgumentNullException(nameof(thresholds));

            _thresholds = new MonitoringAlertThresholds
            {
                MinHitRate = Math.Max(0, Math.Min(1, thresholds.MinHitRate)),
                MaxMemoryUsage = Math.Max(0, Math.Min(1, thresholds.MaxMemoryUsage)),
                MaxEvictionRate = Math.Max(0, thresholds.MaxEvictionRate),
                MaxResponseTimeMs = Math.Max(0, thresholds.MaxResponseTimeMs),
                MinRequestsForHitRateAlert = Math.Max(0, thresholds.MinRequestsForHitRateAlert)
            };

            _logger.LogInformation("Cache alert thresholds updated: {@Thresholds}", _thresholds);
        }

        public Task<CacheMonitoringStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new CacheMonitoringStatus
            {
                LastCheck = _lastStatus.LastCheck,
                IsHealthy = _lastStatus.IsHealthy,
                CurrentHitRate = _lastStatus.CurrentHitRate,
                CurrentMemoryUsagePercent = _lastStatus.CurrentMemoryUsagePercent,
                CurrentEvictionRate = _lastStatus.CurrentEvictionRate,
                CurrentResponseTimeMs = _lastStatus.CurrentResponseTimeMs,
                ActiveAlerts = _lastStatus.ActiveAlerts,
                Details = new Dictionary<string, object>(_lastStatus.Details)
            });
        }

        public IReadOnlyList<CacheAlertEventArgs> GetRecentAlerts(int count = 10)
        {
            _alertHistoryLock.Wait();
            try
            {
                count = Math.Max(1, Math.Min(count, _alertHistory.Count));
                return _alertHistory
                    .OrderByDescending(a => a.Timestamp)
                    .Take(count)
                    .ToList()
                    .AsReadOnly();
            }
            finally
            {
                _alertHistoryLock.Release();
            }
        }

        public void ClearAlertHistory()
        {
            _alertHistoryLock.Wait();
            try
            {
                _alertHistory.Clear();
                _logger.LogInformation("Cache alert history cleared");
            }
            finally
            {
                _alertHistoryLock.Release();
            }
        }
    }
}