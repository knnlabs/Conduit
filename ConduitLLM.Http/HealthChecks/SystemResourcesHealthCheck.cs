using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Http.HealthChecks
{
    /// <summary>
    /// Health check for monitoring system resources (CPU, memory, disk)
    /// </summary>
    public class SystemResourcesHealthCheck : IHealthCheck
    {
        private readonly ILogger<SystemResourcesHealthCheck> _logger;
        private readonly SystemResourcesHealthCheckOptions _options;
        private readonly Process _currentProcess;

        public SystemResourcesHealthCheck(
            ILogger<SystemResourcesHealthCheck> logger,
            IOptions<SystemResourcesHealthCheckOptions> options)
        {
            _logger = logger;
            _options = options.Value;
            _currentProcess = Process.GetCurrentProcess();
        }

        /// <summary>
        /// Check system resources health
        /// </summary>
        public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            var data = new Dictionary<string, object>();
            var issues = new List<string>();
            var isDegraded = false;
            var isUnhealthy = false;

            try
            {
                // Check CPU usage
                var cpuUsage = await GetCpuUsageAsync();
                data["cpuUsagePercent"] = cpuUsage;

                if (cpuUsage > _options.CpuCriticalThreshold)
                {
                    issues.Add($"CPU usage critical: {cpuUsage:F1}%");
                    isUnhealthy = true;
                }
                else if (cpuUsage > _options.CpuWarningThreshold)
                {
                    issues.Add($"CPU usage high: {cpuUsage:F1}%");
                    isDegraded = true;
                }

                // Check memory usage
                var memoryInfo = GetMemoryInfo();
                data["memoryUsageMB"] = memoryInfo.UsedMB;
                data["memoryUsagePercent"] = memoryInfo.UsagePercent;
                data["memoryAvailableMB"] = memoryInfo.AvailableMB;

                if (memoryInfo.UsagePercent > _options.MemoryCriticalThreshold)
                {
                    issues.Add($"Memory usage critical: {memoryInfo.UsagePercent:F1}%");
                    isUnhealthy = true;
                }
                else if (memoryInfo.UsagePercent > _options.MemoryWarningThreshold)
                {
                    issues.Add($"Memory usage high: {memoryInfo.UsagePercent:F1}%");
                    isDegraded = true;
                }

                // Check disk space (if configured)
                if (!string.IsNullOrEmpty(_options.DiskPath))
                {
                    var diskInfo = GetDiskInfo(_options.DiskPath);
                    data["diskUsagePercent"] = diskInfo.UsagePercent;
                    data["diskAvailableGB"] = diskInfo.AvailableGB;
                    data["diskTotalGB"] = diskInfo.TotalGB;

                    if (diskInfo.UsagePercent > _options.DiskCriticalThreshold)
                    {
                        issues.Add($"Disk usage critical: {diskInfo.UsagePercent:F1}%");
                        isUnhealthy = true;
                    }
                    else if (diskInfo.UsagePercent > _options.DiskWarningThreshold)
                    {
                        issues.Add($"Disk usage high: {diskInfo.UsagePercent:F1}%");
                        isDegraded = true;
                    }
                }

                // Check thread count
                var threadCount = _currentProcess.Threads.Count;
                data["threadCount"] = threadCount;

                if (threadCount > _options.ThreadCountThreshold)
                {
                    issues.Add($"Thread count high: {threadCount}");
                    isDegraded = true;
                }

                // Check handle count (Windows only)
                if (OperatingSystem.IsWindows())
                {
                    var handleCount = _currentProcess.HandleCount;
                    data["handleCount"] = handleCount;

                    if (handleCount > _options.HandleCountThreshold)
                    {
                        issues.Add($"Handle count high: {handleCount}");
                        isDegraded = true;
                    }
                }

                // Determine overall health status
                if (isUnhealthy)
                {
                    return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                        $"System resources unhealthy: {string.Join(", ", issues)}",
                        data: data);
                }

                if (isDegraded)
                {
                    return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded(
                        $"System resources degraded: {string.Join(", ", issues)}",
                        data: data);
                }

                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(
                    "System resources healthy",
                    data: data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking system resources");
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                    "System resources health check failed",
                    exception: ex,
                    data: data);
            }
        }

        private async Task<double> GetCpuUsageAsync()
        {
            var startTime = DateTime.UtcNow;
            var startCpuUsage = _currentProcess.TotalProcessorTime;
            
            await Task.Delay(100); // Small delay for measurement
            
            var endTime = DateTime.UtcNow;
            var endCpuUsage = _currentProcess.TotalProcessorTime;
            
            var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
            
            return Math.Round(cpuUsageTotal * 100, 2);
        }

        private MemoryInfo GetMemoryInfo()
        {
            var workingSet = _currentProcess.WorkingSet64;
            var totalMemory = GC.GetTotalMemory(false);
            
            // Get available memory (approximation)
            var gcMemoryInfo = GC.GetGCMemoryInfo();
            var availableMemory = gcMemoryInfo.TotalAvailableMemoryBytes;
            
            return new MemoryInfo
            {
                UsedMB = workingSet / (1024.0 * 1024.0),
                AvailableMB = availableMemory / (1024.0 * 1024.0),
                UsagePercent = (workingSet / (double)availableMemory) * 100
            };
        }

        private DiskInfo GetDiskInfo(string path)
        {
            var driveInfo = new System.IO.DriveInfo(System.IO.Path.GetPathRoot(path) ?? "C:\\");
            
            return new DiskInfo
            {
                AvailableGB = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0),
                TotalGB = driveInfo.TotalSize / (1024.0 * 1024.0 * 1024.0),
                UsagePercent = ((driveInfo.TotalSize - driveInfo.AvailableFreeSpace) / (double)driveInfo.TotalSize) * 100
            };
        }

        private class MemoryInfo
        {
            public double UsedMB { get; set; }
            public double AvailableMB { get; set; }
            public double UsagePercent { get; set; }
        }

        private class DiskInfo
        {
            public double AvailableGB { get; set; }
            public double TotalGB { get; set; }
            public double UsagePercent { get; set; }
        }
    }

    /// <summary>
    /// Options for system resources health check
    /// </summary>
    public class SystemResourcesHealthCheckOptions
    {
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
        /// Disk usage warning threshold (percentage)
        /// </summary>
        public double DiskWarningThreshold { get; set; } = 80;

        /// <summary>
        /// Disk usage critical threshold (percentage)
        /// </summary>
        public double DiskCriticalThreshold { get; set; } = 95;

        /// <summary>
        /// Thread count threshold
        /// </summary>
        public int ThreadCountThreshold { get; set; } = 500;

        /// <summary>
        /// Handle count threshold (Windows only)
        /// </summary>
        public int HandleCountThreshold { get; set; } = 10000;

        /// <summary>
        /// Disk path to monitor (optional)
        /// </summary>
        public string? DiskPath { get; set; }
    }
}