using System;
using System.Collections.Generic;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Performance metrics summary.
    /// </summary>
    public class ImageGenerationPerformanceMetrics
    {
        public double AverageResponseTimeMs { get; set; }
        public double P50ResponseTimeMs { get; set; }
        public double P95ResponseTimeMs { get; set; }
        public double P99ResponseTimeMs { get; set; }
        public Dictionary<string, double> ResponseTimeByProvider { get; set; } = new();
        public Dictionary<string, double> SuccessRateByProvider { get; set; } = new();
        public List<PerformanceTrend> Trends { get; set; } = new();
    }

    /// <summary>
    /// Cost metrics summary.
    /// </summary>
    public class CostMetrics
    {
        public decimal TotalCost { get; set; }
        public decimal AverageCostPerImage { get; set; }
        public Dictionary<string, decimal> CostByProvider { get; set; } = new();
        public Dictionary<string, decimal> CostByModel { get; set; } = new();
        public List<CostTrend> Trends { get; set; } = new();
        public decimal ProjectedMonthlyCost { get; set; }
    }

    /// <summary>
    /// Usage metrics summary.
    /// </summary>
    public class UsageMetrics
    {
        public int TotalRequests { get; set; }
        public int TotalImages { get; set; }
        public Dictionary<string, int> RequestsByProvider { get; set; } = new();
        public Dictionary<string, int> ImageSizeDistribution { get; set; } = new();
        public Dictionary<int, int> RequestsByHour { get; set; } = new();
        public double PeakRequestsPerMinute { get; set; }
    }

    /// <summary>
    /// Quality metrics summary.
    /// </summary>
    public class ImageGenerationQualityMetrics
    {
        public double OverallSuccessRate { get; set; }
        public Dictionary<string, int> ErrorCountByType { get; set; } = new();
        public double SlaCompliancePercent { get; set; }
        public int SlaViolations { get; set; }
        public double AverageQueueWaitTime { get; set; }
    }

    /// <summary>
    /// Current capacity metrics.
    /// </summary>
    public class CapacityMetrics
    {
        public double PeakRequestsPerMinute { get; set; }
        public double AverageRequestsPerMinute { get; set; }
        public double CapacityUtilization { get; set; }
        public int MaxConcurrentGenerations { get; set; }
        public Dictionary<string, double> ResourceUtilization { get; set; } = new();
    }

    /// <summary>
    /// Provider metrics summary.
    /// </summary>
    public class ProviderMetricsSummary
    {
        public double AverageResponseTime { get; set; }
        public double SuccessRate { get; set; }
        public decimal TotalCost { get; set; }
        public int TotalRequests { get; set; }
        public double ErrorRate { get; set; }
    }

    /// <summary>
    /// Performance trend data.
    /// </summary>
    public class PerformanceTrend
    {
        public string Metric { get; set; } = string.Empty;
        public ImageGenerationTrendDirection Direction { get; set; }
        public double ChangePercent { get; set; }
    }

    /// <summary>
    /// Cost trend data.
    /// </summary>
    public class CostTrend
    {
        public DateTime Period { get; set; }
        public decimal Cost { get; set; }
        public double ChangePercent { get; set; }
    }

    /// <summary>
    /// Individual trend data point.
    /// </summary>
    public class UsageTrendPoint
    {
        public DateTime Timestamp { get; set; }
        public int Requests { get; set; }
        public int Images { get; set; }
        public decimal Cost { get; set; }
        public double SuccessRate { get; set; }
        public double AverageResponseTime { get; set; }
    }

    /// <summary>
    /// Trend analysis results.
    /// </summary>
    public class TrendAnalysis
    {
        public ImageGenerationTrendDirection OverallTrend { get; set; }
        public double GrowthRate { get; set; }
        public string PeakUsagePattern { get; set; } = string.Empty;
        public List<string> SeasonalPatterns { get; set; } = new();
        public Dictionary<string, ImageGenerationTrendDirection> ProviderTrends { get; set; } = new();
    }

    /// <summary>
    /// Usage forecast.
    /// </summary>
    public class UsageForecast
    {
        public int ForecastDays { get; set; }
        public decimal ProjectedCost { get; set; }
        public int ProjectedRequests { get; set; }
        public double ConfidenceLevel { get; set; }
        public List<ForecastPoint> ForecastPoints { get; set; } = new();
    }

    /// <summary>
    /// Forecast data point.
    /// </summary>
    public class ForecastPoint
    {
        public DateTime Date { get; set; }
        public int ExpectedRequests { get; set; }
        public int LowerBound { get; set; }
        public int UpperBound { get; set; }
    }

    /// <summary>
    /// Capacity forecast.
    /// </summary>
    public class CapacityForecast
    {
        public DateTime ProjectedCapacityLimit { get; set; }
        public double ProjectedPeakLoad { get; set; }
        public List<CapacityProjection> Projections { get; set; } = new();
    }

    /// <summary>
    /// Capacity projection point.
    /// </summary>
    public class CapacityProjection
    {
        public DateTime Date { get; set; }
        public double ExpectedLoad { get; set; }
        public double CapacityUtilization { get; set; }
        public bool ExceedsCapacity { get; set; }
    }
}