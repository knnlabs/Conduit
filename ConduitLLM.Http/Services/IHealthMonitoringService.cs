using System.Collections.Generic;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs.HealthMonitoring;

namespace ConduitLLM.Http.Services
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
    }
}