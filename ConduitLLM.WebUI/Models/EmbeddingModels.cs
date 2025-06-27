using System;
using System.Collections.Generic;

namespace ConduitLLM.WebUI.Models
{
    /// <summary>
    /// Shared model for embedding provider capability information
    /// </summary>
    public class EmbeddingProviderCapability
    {
        public int Id { get; set; }
        public string ProviderName { get; set; } = string.Empty;
        public bool IsHealthy { get; set; }
        public int ModelCount { get; set; }
        public double AvgLatency { get; set; }
        public double SuccessRate { get; set; }
        public decimal CostPer1K { get; set; }
    }

    /// <summary>
    /// Summary of embedding request for analytics display
    /// </summary>
    public class EmbeddingRequestSummary
    {
        public DateTime Timestamp { get; set; }
        public string VirtualKeyName { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int TokenCount { get; set; }
        public int LatencyMs { get; set; }
        public decimal Cost { get; set; }
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Analytics data for embedding usage
    /// </summary>
    public class EmbeddingAnalytics
    {
        public int TotalRequests { get; set; }
        public decimal TotalCost { get; set; }
        public double AverageLatency { get; set; }
        public double SuccessRate { get; set; }
        public double RequestsTrend { get; set; }
        public double CostTrend { get; set; }
        public double LatencyTrend { get; set; }
        public double SuccessRateTrend { get; set; }
        public List<ModelUsage> TopModels { get; set; } = new();
        public List<VirtualKeyUsage> TopVirtualKeys { get; set; } = new();
        public LatencyDistribution LatencyDistribution { get; set; } = new();
        public ErrorBreakdown ErrorBreakdown { get; set; } = new();
    }

    /// <summary>
    /// Model usage statistics
    /// </summary>
    public class ModelUsage
    {
        public string ModelName { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public int RequestCount { get; set; }
    }

    /// <summary>
    /// Virtual key usage statistics
    /// </summary>
    public class VirtualKeyUsage
    {
        public string KeyName { get; set; } = string.Empty;
        public int RequestCount { get; set; }
        public decimal TotalCost { get; set; }
    }

    /// <summary>
    /// Latency distribution breakdown
    /// </summary>
    public class LatencyDistribution
    {
        public int Under100ms { get; set; }
        public int Between100And200ms { get; set; }
        public int Between200And500ms { get; set; }
        public int Over500ms { get; set; }
    }

    /// <summary>
    /// Error breakdown by type
    /// </summary>
    public class ErrorBreakdown
    {
        public int RateLimitErrors { get; set; }
        public int TimeoutErrors { get; set; }
        public int AuthErrors { get; set; }
        public int OtherErrors { get; set; }
    }

    /// <summary>
    /// Provider-specific embedding settings
    /// </summary>
    public class EmbeddingProviderSettings
    {
        public string DefaultEmbeddingModel { get; set; } = string.Empty;
        public int? DimensionsOverride { get; set; }
        public string InputType { get; set; } = "search_document";
        public string TaskType { get; set; } = "feature-extraction";
        public int? BatchSize { get; set; }
        public int TimeoutSeconds { get; set; } = 30;
        public int MaxRetries { get; set; } = 3;
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
        public DTOs.AlertSeverity Severity { get; set; }

        /// <summary>
        /// Type of alert.
        /// </summary>
        public DTOs.AlertType Type { get; set; }

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

    // AlertSeverity and AlertType enums have been moved to ConduitLLM.WebUI.DTOs namespace
    // to avoid conflicts with the health monitoring DTOs
}