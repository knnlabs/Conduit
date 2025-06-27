using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using ConduitLLM.Http.DTOs.HealthMonitoring;
using ConduitLLM.Http.Hubs;
using Microsoft.Extensions.Caching.Memory;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Service for monitoring system health and performance
    /// </summary>
    public class HealthMonitoringService : IHealthMonitoringService
    {
        private readonly HealthCheckService _healthCheckService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<HealthMonitoringService> _logger;
        private readonly PerformanceCounter? _cpuCounter;
        private readonly PerformanceCounter? _memoryCounter;
        private readonly Process _currentProcess;

        public HealthMonitoringService(
            HealthCheckService healthCheckService,
            IMemoryCache cache,
            ILogger<HealthMonitoringService> logger)
        {
            _healthCheckService = healthCheckService;
            _cache = cache;
            _logger = logger;
            _currentProcess = Process.GetCurrentProcess();

            // Initialize performance counters if on Windows
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                    _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to initialize Windows performance counters");
                }
            }
        }

        /// <summary>
        /// Get comprehensive system health snapshot
        /// </summary>
        public async Task<SystemHealthSnapshot> GetSystemHealthSnapshotAsync()
        {
            var snapshot = new SystemHealthSnapshot
            {
                Timestamp = DateTime.UtcNow,
                Resources = await GetResourceMetricsAsync(),
                Performance = await GetPerformanceMetricsAsync()
            };

            // Get health check results
            var healthReport = await _healthCheckService.CheckHealthAsync();
            
            // Convert health checks to component health
            var components = new List<ComponentHealth>();
            foreach (var entry in healthReport.Entries)
            {
                components.Add(new ComponentHealth
                {
                    Name = entry.Key,
                    Status = ConvertHealthStatus(entry.Value.Status),
                    LastCheck = DateTime.UtcNow,
                    ResponseTimeMs = entry.Value.Duration.TotalMilliseconds,
                    ErrorMessage = entry.Value.Exception?.Message,
                    Metrics = entry.Value.Data?.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value) ?? new Dictionary<string, object>()
                });
            }

            snapshot.Components = components;
            snapshot.OverallStatus = ConvertHealthStatus(healthReport.Status);

            // Get active alerts from cache
            snapshot.ActiveAlerts = _cache.Get<List<HealthAlert>>("active_alerts") ?? new List<HealthAlert>();

            return snapshot;
        }

        /// <summary>
        /// Get health status for specific component
        /// </summary>
        public async Task<ComponentHealth> GetComponentHealthAsync(string componentName)
        {
            var healthReport = await _healthCheckService.CheckHealthAsync();
            
            if (healthReport.Entries.TryGetValue(componentName, out var entry))
            {
                return new ComponentHealth
                {
                    Name = componentName,
                    Status = ConvertHealthStatus(entry.Status),
                    LastCheck = DateTime.UtcNow,
                    ResponseTimeMs = entry.Duration.TotalMilliseconds,
                    ErrorMessage = entry.Exception?.Message,
                    Metrics = entry.Data?.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value) ?? new Dictionary<string, object>()
                };
            }

            return new ComponentHealth
            {
                Name = componentName,
                Status = DTOs.HealthMonitoring.HealthStatus.Unknown,
                LastCheck = DateTime.UtcNow,
                ErrorMessage = "Component not found"
            };
        }

        /// <summary>
        /// Force health check on specific component
        /// </summary>
        public async Task<ComponentHealth> ForceHealthCheckAsync(string componentName)
        {
            // Invalidate cache for this component
            _cache.Remove($"component_health_{componentName}");
            
            return await GetComponentHealthAsync(componentName);
        }

        /// <summary>
        /// Get all component health statuses
        /// </summary>
        public async Task<List<ComponentHealth>> GetAllComponentHealthAsync()
        {
            var healthReport = await _healthCheckService.CheckHealthAsync();
            var components = new List<ComponentHealth>();

            foreach (var entry in healthReport.Entries)
            {
                components.Add(new ComponentHealth
                {
                    Name = entry.Key,
                    Status = ConvertHealthStatus(entry.Value.Status),
                    LastCheck = DateTime.UtcNow,
                    ResponseTimeMs = entry.Value.Duration.TotalMilliseconds,
                    ErrorMessage = entry.Value.Exception?.Message,
                    Metrics = entry.Value.Data?.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value) ?? new Dictionary<string, object>()
                });
            }

            return components.OrderBy(c => c.Name).ToList();
        }

        /// <summary>
        /// Get performance metrics history
        /// </summary>
        public async Task<List<PerformanceMetrics>> GetPerformanceHistoryAsync(DateTime start, DateTime end, TimeSpan interval)
        {
            var history = new List<PerformanceMetrics>();
            
            // In a real implementation, this would query from a time-series database
            // For now, return current metrics
            var currentMetrics = await GetPerformanceMetricsAsync();
            history.Add(currentMetrics);
            
            return history;
        }

        /// <summary>
        /// Get resource metrics history
        /// </summary>
        public async Task<List<ResourceMetrics>> GetResourceHistoryAsync(DateTime start, DateTime end, TimeSpan interval)
        {
            var history = new List<ResourceMetrics>();
            
            // In a real implementation, this would query from a time-series database
            // For now, return current metrics
            var currentMetrics = await GetResourceMetricsAsync();
            history.Add(currentMetrics);
            
            return history;
        }

        public async Task<ResourceMetrics> GetResourceMetricsAsync()
        {
            var metrics = new ResourceMetrics();

            // CPU usage
            if (_cpuCounter != null && OperatingSystem.IsWindows())
            {
                metrics.CpuUsagePercent = _cpuCounter.NextValue();
            }
            else
            {
                // Cross-platform CPU calculation
                var startTime = DateTime.UtcNow;
                var startCpuUsage = _currentProcess.TotalProcessorTime;
                await Task.Delay(100); // Small delay for measurement
                var endTime = DateTime.UtcNow;
                var endCpuUsage = _currentProcess.TotalProcessorTime;
                
                var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
                var totalMsPassed = (endTime - startTime).TotalMilliseconds;
                var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
                
                metrics.CpuUsagePercent = Math.Round(cpuUsageTotal * 100, 2);
            }

            // Memory usage
            var workingSet = _currentProcess.WorkingSet64;
            metrics.MemoryUsageMB = workingSet / (1024.0 * 1024.0);
            
            if (_memoryCounter != null && OperatingSystem.IsWindows())
            {
                var totalMemory = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
                var availableMemory = _memoryCounter.NextValue();
                metrics.MemoryUsagePercent = ((totalMemory - availableMemory) / totalMemory) * 100;
            }
            else
            {
                // Estimate based on GC info
                var totalMemory = GC.GetTotalMemory(false);
                metrics.MemoryUsagePercent = (workingSet / (double)totalMemory) * 100;
            }

            // Thread count
            metrics.ThreadCount = _currentProcess.Threads.Count;

            // Network I/O (placeholder - would need actual network monitoring)
            metrics.NetworkIOMBps = 0;

            // Disk usage (placeholder - would need actual disk monitoring)
            metrics.DiskUsagePercent = 0;

            // Connection pool stats (would be populated from actual connection pools)
            metrics.ConnectionPools = new ConnectionPoolStats
            {
                Database = new PoolStats { Active = 0, Idle = 0, MaxSize = 100, UtilizationPercent = 0 },
                Redis = new PoolStats { Active = 0, Idle = 0, MaxSize = 100, UtilizationPercent = 0 },
                HttpClients = new PoolStats { Active = 0, Idle = 0, MaxSize = 100, UtilizationPercent = 0 }
            };

            return metrics;
        }

        public Task<PerformanceMetrics> GetPerformanceMetricsAsync()
        {
            // This would be populated from actual request tracking
            return Task.FromResult(new PerformanceMetrics
            {
                RequestsPerSecond = 0,
                AverageResponseTimeMs = 0,
                P95ResponseTimeMs = 0,
                P99ResponseTimeMs = 0,
                ErrorRatePercent = 0,
                ActiveRequests = 0,
                QueueDepth = 0
            });
        }

        private DTOs.HealthMonitoring.HealthStatus ConvertHealthStatus(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus status)
        {
            return status switch
            {
                Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy => DTOs.HealthMonitoring.HealthStatus.Healthy,
                Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded => DTOs.HealthMonitoring.HealthStatus.Degraded,
                Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy => DTOs.HealthMonitoring.HealthStatus.Unhealthy,
                _ => DTOs.HealthMonitoring.HealthStatus.Unknown
            };
        }
    }
}