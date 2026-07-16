using ConduitLLM.Configuration.DTOs.HealthMonitoring;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Service for monitoring performance metrics and triggering alerts based on thresholds
    /// </summary>
    public interface IPerformanceMonitoringService
    {
        void RecordRequestMetric(string endpoint, double responseTimeMs, bool isSuccess);
        void RecordDatabaseQueryMetric(string operation, double executionTimeMs);
        void RecordCacheMetric(string operation, bool isHit);
        void RecordConnectionPoolMetric(string poolName, int active, int idle, int waitQueue);
        Task<PerformanceMetrics> GetCurrentMetricsAsync();
        Task<Dictionary<string, EndpointMetrics>> GetEndpointMetricsAsync();
    }
}
