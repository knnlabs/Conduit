namespace ConduitLLM.Configuration.Options
{
    /// <summary>
    /// Configuration options for performance monitoring thresholds and collection intervals
    /// </summary>
    public class PerformanceMonitoringOptions
    {
        // Metrics collection
        public int MaxMetricsRetention { get; set; } = 10000;
        public int MetricsWindowSeconds { get; set; } = 60;
        public int AggregationIntervalSeconds { get; set; } = 30;
        public int ThresholdCheckIntervalSeconds { get; set; } = 30;

        // Response time thresholds
        public double ResponseTimeP95WarningMs { get; set; } = 1000;
        public double ResponseTimeP99CriticalMs { get; set; } = 5000;

        // Error rate thresholds
        public double ErrorRateWarningPercent { get; set; } = 1;
        public double ErrorRateCriticalPercent { get; set; } = 5;

        // Request rate thresholds
        public double RequestRateHighThreshold { get; set; } = 1000;

        // Database thresholds
        public double DatabaseSlowQueryThresholdMs { get; set; } = 1000;
        public int DatabaseSlowQueryCountThreshold { get; set; } = 10;

        // Cache thresholds
        public double CacheHitRateLowThreshold { get; set; } = 80;

        // Connection pool thresholds
        public double ConnectionPoolHighUtilizationThreshold { get; set; } = 80;
        public int ConnectionPoolQueueWarningThreshold { get; set; } = 10;
    }
}
