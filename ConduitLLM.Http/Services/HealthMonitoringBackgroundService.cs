using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ConduitLLM.Configuration.DTOs.HealthMonitoring;
using ConduitLLM.Http.Hubs;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Background service that monitors health checks and triggers alerts
    /// </summary>
    public class HealthMonitoringBackgroundService : BackgroundService
    {
        private readonly HealthCheckService _healthCheckService;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<HealthMonitoringBackgroundService> _logger;
        private readonly HealthMonitoringOptions _options;
        private readonly Dictionary<string, Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus> _previousHealthStatus;
        private readonly Dictionary<string, int> _consecutiveFailures;

        public HealthMonitoringBackgroundService(
            HealthCheckService healthCheckService,
            IServiceScopeFactory serviceScopeFactory,
            ILogger<HealthMonitoringBackgroundService> logger,
            IOptions<HealthMonitoringOptions> options)
        {
            _healthCheckService = healthCheckService;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
            _options = options.Value;
            _previousHealthStatus = new Dictionary<string, Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus>();
            _consecutiveFailures = new Dictionary<string, int>();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Health monitoring background service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await MonitorHealthAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in health monitoring background service");
                }

                await Task.Delay(TimeSpan.FromSeconds(_options.CheckIntervalSeconds), stoppingToken);
            }

            _logger.LogInformation("Health monitoring background service stopped");
        }

        private async Task MonitorHealthAsync(CancellationToken cancellationToken)
        {
            var healthReport = await _healthCheckService.CheckHealthAsync(cancellationToken);

            // Check overall system health
            await CheckOverallHealthAsync(healthReport);

            // Check individual component health
            foreach (var entry in healthReport.Entries)
            {
                await CheckComponentHealthAsync(entry.Key, entry.Value);
            }

            // Check resource metrics
            using var scope = _serviceScopeFactory.CreateScope();
            var healthMonitoringService = scope.ServiceProvider.GetRequiredService<IHealthMonitoringService>();
            var snapshot = await healthMonitoringService.GetSystemHealthSnapshotAsync();
            await CheckResourceMetricsAsync(snapshot.Resources, scope.ServiceProvider);
            await CheckPerformanceMetricsAsync(snapshot.Performance, scope.ServiceProvider);
        }

        private async Task CheckOverallHealthAsync(HealthReport healthReport)
        {
            if (healthReport.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy)
            {
                var unhealthyComponents = healthReport.Entries
                    .Where(e => e.Value.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy)
                    .Select(e => e.Key)
                    .ToList();

                using var scope = _serviceScopeFactory.CreateScope();
                var alertManagementService = scope.ServiceProvider.GetRequiredService<IAlertManagementService>();
                await alertManagementService.TriggerAlertAsync(new HealthAlert
                {
                    Severity = AlertSeverity.Critical,
                    Type = AlertType.ServiceDown,
                    Component = "System",
                    Title = "System Health Critical",
                    Message = $"System is unhealthy. Affected components: {string.Join(", ", unhealthyComponents)}",
                    Context = new Dictionary<string, object>
                    {
                        ["affectedComponents"] = unhealthyComponents,
                        ["totalComponents"] = healthReport.Entries.Count
                    }
                });
            }
        }

        private async Task CheckComponentHealthAsync(string componentName, HealthReportEntry entry)
        {
            var currentStatus = entry.Status;
            
            // Track previous status
            if (!_previousHealthStatus.TryGetValue(componentName, out var previousStatus))
            {
                previousStatus = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy;
            }

            // Update consecutive failures
            if (currentStatus == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy)
            {
                _consecutiveFailures[componentName] = _consecutiveFailures.GetValueOrDefault(componentName) + 1;
            }
            else
            {
                _consecutiveFailures[componentName] = 0;
            }

            // Check if status changed or consecutive failures exceed threshold
            if (currentStatus != previousStatus || 
                (_consecutiveFailures[componentName] >= _options.ConsecutiveFailureThreshold && 
                 _consecutiveFailures[componentName] % _options.ConsecutiveFailureThreshold == 0))
            {
                AlertSeverity severity;
                AlertType alertType;

                switch (currentStatus)
                {
                    case Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy:
                        severity = AlertSeverity.Critical;
                        alertType = AlertType.ServiceDown;
                        break;
                    case Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded:
                        severity = AlertSeverity.Warning;
                        alertType = AlertType.ServiceDegraded;
                        break;
                    default:
                        // Component recovered
                        if (previousStatus == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy || 
                            previousStatus == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded)
                        {
                            using var scope = _serviceScopeFactory.CreateScope();
                            var alertManagementService = scope.ServiceProvider.GetRequiredService<IAlertManagementService>();
                            await alertManagementService.TriggerAlertAsync(new HealthAlert
                            {
                                Severity = AlertSeverity.Info,
                                Type = AlertType.ServiceDegraded,
                                Component = componentName,
                                Title = $"{componentName} Recovered",
                                Message = $"Component {componentName} has recovered and is now healthy",
                                Context = new Dictionary<string, object>
                                {
                                    ["previousStatus"] = previousStatus.ToString(),
                                    ["duration"] = entry.Duration.TotalMilliseconds
                                }
                            });
                        }
                        _previousHealthStatus[componentName] = currentStatus;
                        return;
                }

                using var alertScope = _serviceScopeFactory.CreateScope();
                var alertService = alertScope.ServiceProvider.GetRequiredService<IAlertManagementService>();
                await alertService.TriggerAlertAsync(new HealthAlert
                {
                    Severity = severity,
                    Type = alertType,
                    Component = componentName,
                    Title = $"{componentName} {currentStatus}",
                    Message = entry.Exception?.Message ?? entry.Description ?? $"Component {componentName} is {currentStatus}",
                    Context = new Dictionary<string, object>
                    {
                        ["status"] = currentStatus.ToString(),
                        ["duration"] = entry.Duration.TotalMilliseconds,
                        ["consecutiveFailures"] = _consecutiveFailures[componentName],
                        ["data"] = entry.Data
                    },
                    SuggestedActions = GetSuggestedActions(componentName, entry)
                });
            }

            _previousHealthStatus[componentName] = currentStatus;
        }

        private async Task CheckResourceMetricsAsync(ResourceMetrics resources, IServiceProvider serviceProvider)
        {
            // Check CPU usage
            if (resources.CpuUsagePercent > _options.CpuCriticalThreshold)
            {
                var alertManagementService = serviceProvider.GetRequiredService<IAlertManagementService>();
                await alertManagementService.TriggerAlertAsync(new HealthAlert
                {
                    Severity = AlertSeverity.Critical,
                    Type = AlertType.ResourceExhaustion,
                    Component = "CPU",
                    Title = "Critical CPU Usage",
                    Message = $"CPU usage is critically high: {resources.CpuUsagePercent:F1}%",
                    Context = new Dictionary<string, object>
                    {
                        ["cpuUsage"] = resources.CpuUsagePercent,
                        ["threshold"] = _options.CpuCriticalThreshold
                    },
                    SuggestedActions = new List<string>
                    {
                        "Scale up the service instances",
                        "Check for runaway processes",
                        "Review recent deployments for performance issues"
                    }
                });
            }
            else if (resources.CpuUsagePercent > _options.CpuWarningThreshold)
            {
                var alertManagementService = serviceProvider.GetRequiredService<IAlertManagementService>();
                await alertManagementService.TriggerAlertAsync(new HealthAlert
                {
                    Severity = AlertSeverity.Warning,
                    Type = AlertType.ResourceExhaustion,
                    Component = "CPU",
                    Title = "High CPU Usage",
                    Message = $"CPU usage is high: {resources.CpuUsagePercent:F1}%",
                    Context = new Dictionary<string, object>
                    {
                        ["cpuUsage"] = resources.CpuUsagePercent,
                        ["threshold"] = _options.CpuWarningThreshold
                    }
                });
            }

            // Check memory usage
            if (resources.MemoryUsagePercent > _options.MemoryCriticalThreshold)
            {
                var alertManagementService = serviceProvider.GetRequiredService<IAlertManagementService>();
                await alertManagementService.TriggerAlertAsync(new HealthAlert
                {
                    Severity = AlertSeverity.Critical,
                    Type = AlertType.ResourceExhaustion,
                    Component = "Memory",
                    Title = "Critical Memory Usage",
                    Message = $"Memory usage is critically high: {resources.MemoryUsagePercent:F1}%",
                    Context = new Dictionary<string, object>
                    {
                        ["memoryUsage"] = resources.MemoryUsagePercent,
                        ["memoryUsedMB"] = resources.MemoryUsageMB,
                        ["threshold"] = _options.MemoryCriticalThreshold
                    },
                    SuggestedActions = new List<string>
                    {
                        "Check for memory leaks",
                        "Increase memory allocation",
                        "Review cache usage and eviction policies"
                    }
                });
            }
            else if (resources.MemoryUsagePercent > _options.MemoryWarningThreshold)
            {
                var alertManagementService = serviceProvider.GetRequiredService<IAlertManagementService>();
                await alertManagementService.TriggerAlertAsync(new HealthAlert
                {
                    Severity = AlertSeverity.Warning,
                    Type = AlertType.ResourceExhaustion,
                    Component = "Memory",
                    Title = "High Memory Usage",
                    Message = $"Memory usage is high: {resources.MemoryUsagePercent:F1}%",
                    Context = new Dictionary<string, object>
                    {
                        ["memoryUsage"] = resources.MemoryUsagePercent,
                        ["memoryUsedMB"] = resources.MemoryUsageMB,
                        ["threshold"] = _options.MemoryWarningThreshold
                    }
                });
            }

            // Check connection pools
            await CheckConnectionPoolHealthAsync("Database", resources.ConnectionPools.Database, serviceProvider);
            await CheckConnectionPoolHealthAsync("Redis", resources.ConnectionPools.Redis, serviceProvider);
            await CheckConnectionPoolHealthAsync("HTTP", resources.ConnectionPools.HttpClients, serviceProvider);
        }

        private async Task CheckConnectionPoolHealthAsync(string poolName, PoolStats poolStats, IServiceProvider serviceProvider)
        {
            if (poolStats.UtilizationPercent > _options.ConnectionPoolCriticalThreshold)
            {
                var alertManagementService = serviceProvider.GetRequiredService<IAlertManagementService>();
                await alertManagementService.TriggerAlertAsync(new HealthAlert
                {
                    Severity = AlertSeverity.Critical,
                    Type = AlertType.ResourceExhaustion,
                    Component = $"{poolName} Connection Pool",
                    Title = $"{poolName} Connection Pool Exhaustion",
                    Message = $"{poolName} connection pool is critically high: {poolStats.UtilizationPercent:F1}%",
                    Context = new Dictionary<string, object>
                    {
                        ["active"] = poolStats.Active,
                        ["idle"] = poolStats.Idle,
                        ["maxSize"] = poolStats.MaxSize,
                        ["utilization"] = poolStats.UtilizationPercent,
                        ["waitQueue"] = poolStats.WaitQueueLength
                    },
                    SuggestedActions = new List<string>
                    {
                        $"Increase {poolName} connection pool size",
                        "Check for connection leaks",
                        "Review query performance and optimization"
                    }
                });
            }
        }

        private async Task CheckPerformanceMetricsAsync(PerformanceMetrics performance, IServiceProvider serviceProvider)
        {
            // Check error rate
            if (performance.ErrorRatePercent > _options.ErrorRateCriticalThreshold)
            {
                var alertManagementService = serviceProvider.GetRequiredService<IAlertManagementService>();
                await alertManagementService.TriggerAlertAsync(new HealthAlert
                {
                    Severity = AlertSeverity.Critical,
                    Type = AlertType.PerformanceDegradation,
                    Component = "API",
                    Title = "Critical Error Rate",
                    Message = $"API error rate is critically high: {performance.ErrorRatePercent:F1}%",
                    Context = new Dictionary<string, object>
                    {
                        ["errorRate"] = performance.ErrorRatePercent,
                        ["threshold"] = _options.ErrorRateCriticalThreshold,
                        ["requestsPerSecond"] = performance.RequestsPerSecond
                    },
                    SuggestedActions = new List<string>
                    {
                        "Check application logs for errors",
                        "Review recent deployments",
                        "Check external service dependencies"
                    }
                });
            }

            // Check response times
            if (performance.P99ResponseTimeMs > _options.ResponseTimeCriticalThresholdMs)
            {
                var alertManagementService = serviceProvider.GetRequiredService<IAlertManagementService>();
                await alertManagementService.TriggerAlertAsync(new HealthAlert
                {
                    Severity = AlertSeverity.Critical,
                    Type = AlertType.PerformanceDegradation,
                    Component = "API",
                    Title = "Critical Response Time",
                    Message = $"API P99 response time is critically high: {performance.P99ResponseTimeMs:F0}ms",
                    Context = new Dictionary<string, object>
                    {
                        ["averageResponseTime"] = performance.AverageResponseTimeMs,
                        ["p95ResponseTime"] = performance.P95ResponseTimeMs,
                        ["p99ResponseTime"] = performance.P99ResponseTimeMs,
                        ["threshold"] = _options.ResponseTimeCriticalThresholdMs
                    },
                    SuggestedActions = new List<string>
                    {
                        "Check database query performance",
                        "Review API endpoint optimization",
                        "Check for blocking operations"
                    }
                });
            }
        }

        private List<string> GetSuggestedActions(string componentName, HealthReportEntry entry)
        {
            var actions = new List<string>();

            switch (componentName.ToLower())
            {
                case "database":
                    actions.Add("Check database connection string");
                    actions.Add("Verify database server is accessible");
                    actions.Add("Check for database locks or long-running queries");
                    break;
                case "redis":
                    actions.Add("Check Redis connection string");
                    actions.Add("Verify Redis server is running");
                    actions.Add("Check Redis memory usage");
                    break;
                case "rabbitmq":
                    actions.Add("Check RabbitMQ connection settings");
                    actions.Add("Verify RabbitMQ server is accessible");
                    actions.Add("Check message queue depth");
                    break;
                case "providers":
                    actions.Add("Check provider API keys");
                    actions.Add("Verify provider endpoints are accessible");
                    actions.Add("Review provider rate limits");
                    break;
                default:
                    actions.Add($"Check {componentName} configuration");
                    actions.Add($"Review {componentName} logs for errors");
                    break;
            }

            return actions;
        }
    }

    /// <summary>
    /// Options for health monitoring
    /// </summary>
    public class HealthMonitoringOptions
    {
        /// <summary>
        /// Interval between health checks in seconds
        /// </summary>
        public int CheckIntervalSeconds { get; set; } = 30;

        /// <summary>
        /// Number of consecutive failures before re-alerting
        /// </summary>
        public int ConsecutiveFailureThreshold { get; set; } = 3;

        /// <summary>
        /// CPU usage warning threshold (percentage)
        /// </summary>
        public double CpuWarningThreshold { get; set; } = 70;

        /// <summary>
        /// CPU usage critical threshold (percentage)
        /// </summary>
        public double CpuCriticalThreshold { get; set; } = 90;

        /// <summary>
        /// Memory usage warning threshold (percentage)
        /// </summary>
        public double MemoryWarningThreshold { get; set; } = 70;

        /// <summary>
        /// Memory usage critical threshold (percentage)
        /// </summary>
        public double MemoryCriticalThreshold { get; set; } = 90;

        /// <summary>
        /// Connection pool critical threshold (percentage)
        /// </summary>
        public double ConnectionPoolCriticalThreshold { get; set; } = 80;

        /// <summary>
        /// Error rate critical threshold (percentage)
        /// </summary>
        public double ErrorRateCriticalThreshold { get; set; } = 5;

        /// <summary>
        /// Response time critical threshold (milliseconds)
        /// </summary>
        public int ResponseTimeCriticalThresholdMs { get; set; } = 5000;
    }
}