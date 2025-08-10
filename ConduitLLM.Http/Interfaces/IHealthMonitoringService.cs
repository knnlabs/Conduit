using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs.HealthMonitoring;

namespace ConduitLLM.Http.Interfaces
{
    /// <summary>
    /// Interface for monitoring system health and performance
    /// </summary>
    public interface IHealthMonitoringService
    {
        /// <summary>
        /// Get comprehensive system health snapshot
        /// </summary>
        Task<SystemHealthSnapshot> GetSystemHealthSnapshotAsync();

        /// <summary>
        /// Get component health details
        /// </summary>
        Task<ComponentHealth> GetComponentHealthAsync(string componentName);

        /// <summary>
        /// Get all component health statuses
        /// </summary>
        Task<List<ComponentHealth>> GetAllComponentHealthAsync();

        /// <summary>
        /// Get resource metrics
        /// </summary>
        Task<ResourceMetrics> GetResourceMetricsAsync();

        /// <summary>
        /// Get performance metrics
        /// </summary>
        Task<PerformanceMetrics> GetPerformanceMetricsAsync();

        /// <summary>
        /// Force health check on specific component
        /// </summary>
        Task<ComponentHealth> ForceHealthCheckAsync(string componentName);

        /// <summary>
        /// Get performance metrics history
        /// </summary>
        Task<List<PerformanceMetrics>> GetPerformanceHistoryAsync(DateTime start, DateTime end, TimeSpan interval);

        /// <summary>
        /// Get resource metrics history
        /// </summary>
        Task<List<ResourceMetrics>> GetResourceHistoryAsync(DateTime start, DateTime end, TimeSpan interval);
    }
}