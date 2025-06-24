using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Service for monitoring embedding operations and providing alerts.
    /// </summary>
    public interface IEmbeddingMonitoringService
    {
        /// <summary>
        /// Records metrics for a successful embedding request.
        /// </summary>
        /// <param name="modelName">The model used for the embedding.</param>
        /// <param name="latencyMs">The request latency in milliseconds.</param>
        /// <param name="inputTokens">Number of input tokens.</param>
        /// <param name="cost">Cost of the request.</param>
        /// <param name="cacheHit">Whether the request was served from cache.</param>
        Task RecordSuccessAsync(string modelName, long latencyMs, int inputTokens, decimal cost, bool cacheHit = false);

        /// <summary>
        /// Records metrics for a failed embedding request.
        /// </summary>
        /// <param name="modelName">The model that failed.</param>
        /// <param name="errorType">Type of error that occurred.</param>
        /// <param name="errorMessage">Error message.</param>
        /// <param name="latencyMs">The request latency before failure.</param>
        Task RecordFailureAsync(string modelName, string errorType, string? errorMessage, long latencyMs);

        /// <summary>
        /// Gets real-time metrics for embedding operations.
        /// </summary>
        /// <param name="timeWindow">Time window for metrics (e.g., last 1 hour).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Real-time embedding metrics.</returns>
        Task<EmbeddingMetrics> GetMetricsAsync(TimeSpan timeWindow, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets health status for all embedding models.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Health status for each model.</returns>
        Task<IReadOnlyList<EmbeddingModelHealth>> GetModelHealthAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if any models require attention (high latency, errors, etc.).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of alerts for models that need attention.</returns>
        Task<IReadOnlyList<EmbeddingAlert>> GetActiveAlertsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets detailed performance statistics for a specific model.
        /// </summary>
        /// <param name="modelName">The model to get statistics for.</param>
        /// <param name="timeWindow">Time window for statistics.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Detailed model performance statistics.</returns>
        Task<EmbeddingModelStatistics> GetModelStatisticsAsync(string modelName, TimeSpan timeWindow, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets cost analysis for embedding operations.
        /// </summary>
        /// <param name="timeWindow">Time window for cost analysis.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Cost breakdown and analysis.</returns>
        Task<EmbeddingCostAnalysis> GetCostAnalysisAsync(TimeSpan timeWindow, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Comprehensive metrics for embedding operations.
    /// </summary>
    public class EmbeddingMetrics
    {
        /// <summary>
        /// Total number of embedding requests.
        /// </summary>
        public long TotalRequests { get; set; }

        /// <summary>
        /// Number of successful requests.
        /// </summary>
        public long SuccessfulRequests { get; set; }

        /// <summary>
        /// Number of failed requests.
        /// </summary>
        public long FailedRequests { get; set; }

        /// <summary>
        /// Number of requests served from cache.
        /// </summary>
        public long CacheHits { get; set; }

        /// <summary>
        /// Success rate as a percentage.
        /// </summary>
        public double SuccessRate => TotalRequests > 0 ? (double)SuccessfulRequests / TotalRequests * 100 : 0;

        /// <summary>
        /// Cache hit rate as a percentage.
        /// </summary>
        public double CacheHitRate => TotalRequests > 0 ? (double)CacheHits / TotalRequests * 100 : 0;

        /// <summary>
        /// Average latency in milliseconds.
        /// </summary>
        public double AverageLatencyMs { get; set; }

        /// <summary>
        /// 95th percentile latency in milliseconds.
        /// </summary>
        public double P95LatencyMs { get; set; }

        /// <summary>
        /// 99th percentile latency in milliseconds.
        /// </summary>
        public double P99LatencyMs { get; set; }

        /// <summary>
        /// Total cost for the time period.
        /// </summary>
        public decimal TotalCost { get; set; }

        /// <summary>
        /// Cost savings from cache hits.
        /// </summary>
        public decimal CacheSavings { get; set; }

        /// <summary>
        /// Requests per second rate.
        /// </summary>
        public double RequestsPerSecond { get; set; }

        /// <summary>
        /// Time window for these metrics.
        /// </summary>
        public TimeSpan TimeWindow { get; set; }

        /// <summary>
        /// When these metrics were calculated.
        /// </summary>
        public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Health status for an embedding model.
    /// </summary>
    public class EmbeddingModelHealth
    {
        /// <summary>
        /// Name of the model.
        /// </summary>
        public string ModelName { get; set; } = string.Empty;

        /// <summary>
        /// Whether the model is healthy.
        /// </summary>
        public bool IsHealthy { get; set; }

        /// <summary>
        /// Current health status description.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Last successful request timestamp.
        /// </summary>
        public DateTime? LastSuccessfulRequest { get; set; }

        /// <summary>
        /// Recent error rate (last hour).
        /// </summary>
        public double RecentErrorRate { get; set; }

        /// <summary>
        /// Recent average latency.
        /// </summary>
        public double RecentAverageLatencyMs { get; set; }

        /// <summary>
        /// Number of requests in the last hour.
        /// </summary>
        public long RecentRequestCount { get; set; }

        /// <summary>
        /// Provider hosting this model.
        /// </summary>
        public string Provider { get; set; } = string.Empty;
    }

    /// <summary>
    /// Alert for embedding model issues.
    /// </summary>
    public class EmbeddingAlert
    {
        /// <summary>
        /// Unique identifier for the alert.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Model name associated with the alert.
        /// </summary>
        public string ModelName { get; set; } = string.Empty;

        /// <summary>
        /// Alert severity level.
        /// </summary>
        public AlertSeverity Severity { get; set; }

        /// <summary>
        /// Type of alert.
        /// </summary>
        public EmbeddingAlertType Type { get; set; }

        /// <summary>
        /// Alert title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Detailed alert message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// When the alert was first triggered.
        /// </summary>
        public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Current metric value that triggered the alert.
        /// </summary>
        public double? CurrentValue { get; set; }

        /// <summary>
        /// Threshold value that was exceeded.
        /// </summary>
        public double? ThresholdValue { get; set; }

        /// <summary>
        /// Recommended action to resolve the alert.
        /// </summary>
        public string? RecommendedAction { get; set; }
    }

    /// <summary>
    /// Types of alerts for embedding services.
    /// </summary>
    public enum EmbeddingAlertType
    {
        HighLatency,
        HighErrorRate,
        ModelUnavailable,
        HighCost,
        LowCacheHitRate,
        RateLimitExceeded,
        UnusualTrafficPattern
    }

    /// <summary>
    /// Detailed statistics for a specific embedding model.
    /// </summary>
    public class EmbeddingModelStatistics
    {
        /// <summary>
        /// Model name.
        /// </summary>
        public string ModelName { get; set; } = string.Empty;

        /// <summary>
        /// Total requests for this model.
        /// </summary>
        public long TotalRequests { get; set; }

        /// <summary>
        /// Successful requests.
        /// </summary>
        public long SuccessfulRequests { get; set; }

        /// <summary>
        /// Failed requests.
        /// </summary>
        public long FailedRequests { get; set; }

        /// <summary>
        /// Requests served from cache.
        /// </summary>
        public long CacheHits { get; set; }

        /// <summary>
        /// Average latency in milliseconds.
        /// </summary>
        public double AverageLatencyMs { get; set; }

        /// <summary>
        /// Latency percentiles.
        /// </summary>
        public LatencyPercentiles Latency { get; set; } = new();

        /// <summary>
        /// Total cost for this model.
        /// </summary>
        public decimal TotalCost { get; set; }

        /// <summary>
        /// Total tokens processed.
        /// </summary>
        public long TotalTokens { get; set; }

        /// <summary>
        /// Error breakdown by type.
        /// </summary>
        public Dictionary<string, int> ErrorBreakdown { get; set; } = new();

        /// <summary>
        /// Time window for these statistics.
        /// </summary>
        public TimeSpan TimeWindow { get; set; }
    }

    /// <summary>
    /// Latency percentile measurements.
    /// </summary>
    public class LatencyPercentiles
    {
        public double P50 { get; set; }
        public double P90 { get; set; }
        public double P95 { get; set; }
        public double P99 { get; set; }
        public double P999 { get; set; }
    }

    /// <summary>
    /// Cost analysis for embedding operations.
    /// </summary>
    public class EmbeddingCostAnalysis
    {
        /// <summary>
        /// Total cost for the period.
        /// </summary>
        public decimal TotalCost { get; set; }

        /// <summary>
        /// Cost breakdown by model.
        /// </summary>
        public Dictionary<string, decimal> CostByModel { get; set; } = new();

        /// <summary>
        /// Cost breakdown by provider.
        /// </summary>
        public Dictionary<string, decimal> CostByProvider { get; set; } = new();

        /// <summary>
        /// Estimated cost without caching.
        /// </summary>
        public decimal EstimatedCostWithoutCache { get; set; }

        /// <summary>
        /// Cost savings from caching.
        /// </summary>
        public decimal CacheSavings => EstimatedCostWithoutCache - TotalCost;

        /// <summary>
        /// Average cost per request.
        /// </summary>
        public decimal AverageCostPerRequest { get; set; }

        /// <summary>
        /// Most expensive model.
        /// </summary>
        public string? MostExpensiveModel { get; set; }

        /// <summary>
        /// Most cost-effective model.
        /// </summary>
        public string? MostCostEffectiveModel { get; set; }

        /// <summary>
        /// Projected monthly cost based on current usage.
        /// </summary>
        public decimal ProjectedMonthlyCost { get; set; }

        /// <summary>
        /// Time window for this analysis.
        /// </summary>
        public TimeSpan TimeWindow { get; set; }
    }
}