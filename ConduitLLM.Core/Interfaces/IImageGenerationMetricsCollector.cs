using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Collects comprehensive metrics for image generation operations.
    /// </summary>
    public interface IImageGenerationMetricsCollector
    {
        /// <summary>
        /// Records the start of an image generation operation.
        /// </summary>
        /// <param name="operationId">Unique operation identifier.</param>
        /// <param name="provider">Provider name.</param>
        /// <param name="model">Model name.</param>
        /// <param name="imageCount">Number of images requested.</param>
        /// <param name="virtualKeyId">Virtual key ID.</param>
        void RecordGenerationStart(string operationId, string provider, string model, int imageCount, int virtualKeyId);

        /// <summary>
        /// Records the completion of an image generation operation.
        /// </summary>
        /// <param name="operationId">Unique operation identifier.</param>
        /// <param name="success">Whether the operation succeeded.</param>
        /// <param name="imagesGenerated">Number of images successfully generated.</param>
        /// <param name="cost">Total cost of the operation.</param>
        /// <param name="error">Error message if failed.</param>
        void RecordGenerationComplete(string operationId, bool success, int imagesGenerated, decimal cost, string? error = null);

        /// <summary>
        /// Records provider-specific performance metrics.
        /// </summary>
        /// <param name="provider">Provider name.</param>
        /// <param name="model">Model name.</param>
        /// <param name="responseTimeMs">Response time in milliseconds.</param>
        /// <param name="queueTimeMs">Time spent in queue in milliseconds.</param>
        void RecordProviderPerformance(string provider, string model, double responseTimeMs, double queueTimeMs);

        /// <summary>
        /// Records image download metrics.
        /// </summary>
        /// <param name="provider">Provider name.</param>
        /// <param name="downloadTimeMs">Download time in milliseconds.</param>
        /// <param name="imageSizeBytes">Size of the image in bytes.</param>
        /// <param name="success">Whether the download succeeded.</param>
        void RecordImageDownload(string provider, double downloadTimeMs, long imageSizeBytes, bool success);

        /// <summary>
        /// Records storage operation metrics.
        /// </summary>
        /// <param name="storageType">Type of storage (S3, InMemory, etc).</param>
        /// <param name="operationType">Type of operation (Store, Retrieve, Delete).</param>
        /// <param name="durationMs">Duration in milliseconds.</param>
        /// <param name="sizeBytes">Size in bytes.</param>
        /// <param name="success">Whether the operation succeeded.</param>
        void RecordStorageOperation(string storageType, string operationType, double durationMs, long sizeBytes, bool success);

        /// <summary>
        /// Records queue metrics.
        /// </summary>
        /// <param name="queueName">Name of the queue.</param>
        /// <param name="depth">Current queue depth.</param>
        /// <param name="oldestItemAgeMs">Age of the oldest item in milliseconds.</param>
        void RecordQueueMetrics(string queueName, int depth, double oldestItemAgeMs);

        /// <summary>
        /// Records resource utilization during image generation.
        /// </summary>
        /// <param name="cpuPercent">CPU usage percentage.</param>
        /// <param name="memoryMb">Memory usage in MB.</param>
        /// <param name="activeGenerations">Number of active generations.</param>
        /// <param name="threadPoolThreads">Number of thread pool threads.</param>
        void RecordResourceUtilization(double cpuPercent, double memoryMb, int activeGenerations, int threadPoolThreads);

        /// <summary>
        /// Records virtual key usage metrics.
        /// </summary>
        /// <param name="virtualKeyId">Virtual key ID.</param>
        /// <param name="imagesGenerated">Number of images generated.</param>
        /// <param name="cost">Cost incurred.</param>
        /// <param name="remainingBudget">Remaining budget.</param>
        void RecordVirtualKeyUsage(int virtualKeyId, int imagesGenerated, decimal cost, decimal remainingBudget);

        /// <summary>
        /// Records provider health score.
        /// </summary>
        /// <param name="provider">Provider name.</param>
        /// <param name="healthScore">Health score (0-1).</param>
        /// <param name="isHealthy">Whether the provider is considered healthy.</param>
        /// <param name="lastError">Last error message if any.</param>
        void RecordProviderHealth(string provider, double healthScore, bool isHealthy, string? lastError = null);

        /// <summary>
        /// Records model-specific metrics.
        /// </summary>
        /// <param name="model">Model name.</param>
        /// <param name="imageSize">Image size requested.</param>
        /// <param name="quality">Quality setting.</param>
        /// <param name="generationTimeMs">Generation time in milliseconds.</param>
        void RecordModelMetrics(string model, string imageSize, string quality, double generationTimeMs);

        /// <summary>
        /// Gets current metrics snapshot.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Current metrics snapshot.</returns>
        Task<ImageGenerationMetricsSnapshot> GetMetricsSnapshotAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets provider-specific metrics.
        /// </summary>
        /// <param name="provider">Provider name.</param>
        /// <param name="timeWindowMinutes">Time window in minutes.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Provider-specific metrics.</returns>
        Task<ProviderMetricsSummary> GetProviderMetricsAsync(string provider, int timeWindowMinutes = 60, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets SLA compliance metrics.
        /// </summary>
        /// <param name="timeWindowHours">Time window in hours.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>SLA compliance summary.</returns>
        Task<SlaComplianceSummary> GetSlaComplianceAsync(int timeWindowHours = 24, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Snapshot of current image generation metrics.
    /// </summary>
    public class ImageGenerationMetricsSnapshot
    {
        public DateTime Timestamp { get; set; }
        public int ActiveGenerations { get; set; }
        public double GenerationsPerMinute { get; set; }
        public double AverageResponseTimeMs { get; set; }
        public double P95ResponseTimeMs { get; set; }
        public double SuccessRate { get; set; }
        public Dictionary<string, ProviderStatus> ProviderStatuses { get; set; } = new();
        public QueueMetrics QueueMetrics { get; set; } = new();
        public ResourceMetrics ResourceMetrics { get; set; } = new();
        public Dictionary<string, int> ErrorCounts { get; set; } = new();
        public decimal TotalCostLastHour { get; set; }
        public int TotalImagesLastHour { get; set; }
    }


    /// <summary>
    /// Model-specific metrics.
    /// </summary>
    public class ModelMetrics
    {
        public string ModelName { get; set; } = string.Empty;
        public int RequestCount { get; set; }
        public double AverageResponseTimeMs { get; set; }
        public decimal TotalCost { get; set; }
        public int TotalImages { get; set; }
        public Dictionary<string, int> SizeDistribution { get; set; } = new();
        public Dictionary<string, int> QualityDistribution { get; set; } = new();
    }

    /// <summary>
    /// Provider status information.
    /// </summary>
    public class ProviderStatus
    {
        public bool IsHealthy { get; set; }
        public double HealthScore { get; set; }
        public int ActiveRequests { get; set; }
        public double AverageResponseTimeMs { get; set; }
        public int ConsecutiveFailures { get; set; }
        public DateTime? LastSuccessAt { get; set; }
        public DateTime? LastFailureAt { get; set; }
        public string? LastError { get; set; }
    }

    /// <summary>
    /// Queue metrics.
    /// </summary>
    public class QueueMetrics
    {
        public int TotalDepth { get; set; }
        public Dictionary<string, int> QueueDepthByPriority { get; set; } = new();
        public double AverageWaitTimeMs { get; set; }
        public double MaxWaitTimeMs { get; set; }
        public int ProcessingRate { get; set; }
        public DateTime? OldestItemTimestamp { get; set; }
    }

    /// <summary>
    /// Resource utilization metrics.
    /// </summary>
    public class ResourceMetrics
    {
        public double CpuUsagePercent { get; set; }
        public double MemoryUsageMb { get; set; }
        public double MemoryUsagePercent { get; set; }
        public int ThreadPoolThreads { get; set; }
        public int ActiveConnections { get; set; }
        public double StorageUsedGb { get; set; }
        public double StorageBandwidthMbps { get; set; }
    }

    /// <summary>
    /// SLA compliance summary.
    /// </summary>
    public class SlaComplianceSummary
    {
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public double AvailabilityPercent { get; set; }
        public bool MeetsAvailabilitySla { get; set; }
        public double P95ResponseTimeMs { get; set; }
        public bool MeetsResponseTimeSla { get; set; }
        public double ErrorRatePercent { get; set; }
        public bool MeetsErrorRateSla { get; set; }
        public int TotalRequests { get; set; }
        public int SuccessfulRequests { get; set; }
        public int FailedRequests { get; set; }
        public List<SlaViolation> Violations { get; set; } = new();
    }

    /// <summary>
    /// SLA violation record.
    /// </summary>
    public class SlaViolation
    {
        public DateTime Timestamp { get; set; }
        public string ViolationType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double ActualValue { get; set; }
        public double ThresholdValue { get; set; }
        public TimeSpan Duration { get; set; }
        public string? AffectedProvider { get; set; }
    }
}