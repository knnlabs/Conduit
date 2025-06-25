using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Service for analyzing image generation performance and providing insights.
    /// </summary>
    public interface IImageGenerationAnalyticsService
    {
        /// <summary>
        /// Gets a comprehensive analytics report for a time period.
        /// </summary>
        /// <param name="startTime">Start time for the report.</param>
        /// <param name="endTime">End time for the report.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Comprehensive analytics report.</returns>
        Task<ImageGenerationAnalyticsReport> GetAnalyticsReportAsync(
            DateTime startTime,
            DateTime endTime,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets provider comparison analytics.
        /// </summary>
        /// <param name="timeWindowHours">Time window in hours.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Provider comparison report.</returns>
        Task<ProviderComparisonReport> GetProviderComparisonAsync(
            int timeWindowHours = 24,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets cost optimization recommendations.
        /// </summary>
        /// <param name="timeWindowDays">Time window in days to analyze.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Cost optimization recommendations.</returns>
        Task<CostOptimizationReport> GetCostOptimizationRecommendationsAsync(
            int timeWindowDays = 7,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets usage trend analysis.
        /// </summary>
        /// <param name="granularity">Time granularity (hourly, daily, weekly).</param>
        /// <param name="periods">Number of periods to analyze.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Usage trend report.</returns>
        Task<UsageTrendReport> GetUsageTrendsAsync(
            TimeGranularity granularity,
            int periods = 30,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets error analysis and patterns.
        /// </summary>
        /// <param name="timeWindowHours">Time window in hours.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Error analysis report.</returns>
        Task<ErrorAnalysisReport> GetErrorAnalysisAsync(
            int timeWindowHours = 24,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets capacity planning insights.
        /// </summary>
        /// <param name="forecastDays">Days to forecast ahead.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Capacity planning report.</returns>
        Task<CapacityPlanningReport> GetCapacityPlanningInsightsAsync(
            int forecastDays = 30,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets virtual key usage analytics.
        /// </summary>
        /// <param name="virtualKeyId">Optional virtual key ID filter.</param>
        /// <param name="timeWindowDays">Time window in days.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Virtual key usage report.</returns>
        Task<VirtualKeyUsageReport> GetVirtualKeyUsageAnalyticsAsync(
            int? virtualKeyId = null,
            int timeWindowDays = 30,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets performance anomaly detection results.
        /// </summary>
        /// <param name="timeWindowHours">Time window in hours.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Anomaly detection report.</returns>
        Task<AnomalyDetectionReport> DetectPerformanceAnomaliesAsync(
            int timeWindowHours = 24,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Comprehensive analytics report for image generation.
    /// </summary>
    public class ImageGenerationAnalyticsReport
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public ExecutiveSummary Summary { get; set; } = new();
        public ImageGenerationPerformanceMetrics Performance { get; set; } = new();
        public CostMetrics Cost { get; set; } = new();
        public UsageMetrics Usage { get; set; } = new();
        public ImageGenerationQualityMetrics Quality { get; set; } = new();
        public List<KeyInsight> KeyInsights { get; set; } = new();
    }

    /// <summary>
    /// Executive summary of analytics.
    /// </summary>
    public class ExecutiveSummary
    {
        public int TotalGenerations { get; set; }
        public int TotalImages { get; set; }
        public decimal TotalCost { get; set; }
        public double OverallSuccessRate { get; set; }
        public double AverageResponseTime { get; set; }
        public int UniqueVirtualKeys { get; set; }
        public Dictionary<string, int> TopProviders { get; set; } = new();
        public List<string> CriticalIssues { get; set; } = new();
    }

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
    /// Key insight from analytics.
    /// </summary>
    public class KeyInsight
    {
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public InsightSeverity Severity { get; set; }
        public string? Recommendation { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();
    }

    /// <summary>
    /// Insight severity levels.
    /// </summary>
    public enum InsightSeverity
    {
        Info,
        Opportunity,
        Warning,
        Critical
    }

    /// <summary>
    /// Provider comparison report.
    /// </summary>
    public class ProviderComparisonReport
    {
        public DateTime GeneratedAt { get; set; }
        public int TimeWindowHours { get; set; }
        public List<ProviderComparison> Providers { get; set; } = new();
        public string BestPerformanceProvider { get; set; } = string.Empty;
        public string BestCostEfficiencyProvider { get; set; } = string.Empty;
        public string MostReliableProvider { get; set; } = string.Empty;
        public List<ProviderRecommendation> Recommendations { get; set; } = new();
    }

    /// <summary>
    /// Individual provider comparison data.
    /// </summary>
    public class ProviderComparison
    {
        public string ProviderName { get; set; } = string.Empty;
        public double PerformanceScore { get; set; }
        public double ReliabilityScore { get; set; }
        public double CostEfficiencyScore { get; set; }
        public double OverallScore { get; set; }
        public ProviderMetricsSummary Metrics { get; set; } = new();
        public List<string> Strengths { get; set; } = new();
        public List<string> Weaknesses { get; set; } = new();
    }

    /// <summary>
    /// Provider recommendation.
    /// </summary>
    public class ProviderRecommendation
    {
        public string Provider { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public double ExpectedImprovement { get; set; }
    }

    /// <summary>
    /// Cost optimization report.
    /// </summary>
    public class CostOptimizationReport
    {
        public DateTime GeneratedAt { get; set; }
        public decimal CurrentMonthlyCost { get; set; }
        public decimal PotentialSavings { get; set; }
        public double SavingsPercentage { get; set; }
        public List<CostOptimizationOpportunity> Opportunities { get; set; } = new();
        public Dictionary<string, decimal> CostByVirtualKey { get; set; } = new();
        public List<CostAnomaly> Anomalies { get; set; } = new();
    }

    /// <summary>
    /// Cost optimization opportunity.
    /// </summary>
    public class CostOptimizationOpportunity
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal PotentialSavings { get; set; }
        public string Implementation { get; set; } = string.Empty;
        public OpportunityImpact Impact { get; set; }
        public OpportunityEffort Effort { get; set; }
    }

    /// <summary>
    /// Impact level of optimization opportunity.
    /// </summary>
    public enum OpportunityImpact
    {
        Low,
        Medium,
        High
    }

    /// <summary>
    /// Implementation effort required.
    /// </summary>
    public enum OpportunityEffort
    {
        Low,
        Medium,
        High
    }

    /// <summary>
    /// Cost anomaly detection.
    /// </summary>
    public class CostAnomaly
    {
        public DateTime DetectedAt { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal AnomalousAmount { get; set; }
        public decimal ExpectedAmount { get; set; }
        public double DeviationPercent { get; set; }
    }

    /// <summary>
    /// Usage trend report.
    /// </summary>
    public class UsageTrendReport
    {
        public DateTime GeneratedAt { get; set; }
        public TimeGranularity Granularity { get; set; }
        public List<UsageTrendPoint> TrendData { get; set; } = new();
        public TrendAnalysis Analysis { get; set; } = new();
        public UsageForecast Forecast { get; set; } = new();
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
    /// Time granularity for analysis.
    /// </summary>
    public enum TimeGranularity
    {
        Hourly,
        Daily,
        Weekly,
        Monthly
    }

    /// <summary>
    /// Error analysis report.
    /// </summary>
    public class ErrorAnalysisReport
    {
        public DateTime GeneratedAt { get; set; }
        public int TotalErrors { get; set; }
        public double ErrorRate { get; set; }
        public Dictionary<string, ErrorPattern> ErrorPatterns { get; set; } = new();
        public List<ErrorCorrelation> Correlations { get; set; } = new();
        public Dictionary<string, List<ErrorOccurrence>> TopErrors { get; set; } = new();
        public List<string> RootCauseAnalysis { get; set; } = new();
    }

    /// <summary>
    /// Error pattern analysis.
    /// </summary>
    public class ErrorPattern
    {
        public string ErrorType { get; set; } = string.Empty;
        public int Occurrences { get; set; }
        public double Frequency { get; set; }
        public List<string> AffectedProviders { get; set; } = new();
        public string Pattern { get; set; } = string.Empty;
        public bool IsRetryable { get; set; }
    }

    /// <summary>
    /// Error correlation finding.
    /// </summary>
    public class ErrorCorrelation
    {
        public string Factor { get; set; } = string.Empty;
        public double CorrelationStrength { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Individual error occurrence.
    /// </summary>
    public class ErrorOccurrence
    {
        public DateTime Timestamp { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public Dictionary<string, object> Context { get; set; } = new();
    }

    /// <summary>
    /// Capacity planning report.
    /// </summary>
    public class CapacityPlanningReport
    {
        public DateTime GeneratedAt { get; set; }
        public CapacityMetrics CurrentCapacity { get; set; } = new();
        public CapacityForecast Forecast { get; set; } = new();
        public List<CapacityRecommendation> Recommendations { get; set; } = new();
        public Dictionary<string, ProviderCapacity> ProviderCapacities { get; set; } = new();
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

    /// <summary>
    /// Capacity recommendation.
    /// </summary>
    public class CapacityRecommendation
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public CapacityAction Action { get; set; }
        public DateTime RecommendedBy { get; set; }
        public string Justification { get; set; } = string.Empty;
    }

    /// <summary>
    /// Capacity action type.
    /// </summary>
    public enum CapacityAction
    {
        ScaleUp,
        ScaleOut,
        OptimizeUsage,
        AddProvider,
        ImplementCaching,
        EnableRateLimiting
    }

    /// <summary>
    /// Provider capacity information.
    /// </summary>
    public class ProviderCapacity
    {
        public string ProviderName { get; set; } = string.Empty;
        public int RateLimitPerMinute { get; set; }
        public double CurrentUtilization { get; set; }
        public int AvailableCapacity { get; set; }
        public bool IsAtCapacity { get; set; }
    }

    /// <summary>
    /// Virtual key usage report.
    /// </summary>
    public class VirtualKeyUsageReport
    {
        public DateTime GeneratedAt { get; set; }
        public List<VirtualKeyUsage> KeyUsages { get; set; } = new();
        public decimal TotalSpend { get; set; }
        public Dictionary<int, UsageTrend> UsageTrends { get; set; } = new();
        public List<BudgetAlert> BudgetAlerts { get; set; } = new();
    }

    /// <summary>
    /// Individual virtual key usage.
    /// </summary>
    public class VirtualKeyUsage
    {
        public int VirtualKeyId { get; set; }
        public string KeyName { get; set; } = string.Empty;
        public int TotalRequests { get; set; }
        public int TotalImages { get; set; }
        public decimal TotalCost { get; set; }
        public decimal BudgetRemaining { get; set; }
        public double BudgetUtilization { get; set; }
        public DateTime LastUsed { get; set; }
        public Dictionary<string, int> RequestsByProvider { get; set; } = new();
    }

    /// <summary>
    /// Usage trend for a virtual key.
    /// </summary>
    public class UsageTrend
    {
        public ImageGenerationTrendDirection Direction { get; set; }
        public double GrowthRate { get; set; }
        public DateTime ProjectedBudgetExhaustion { get; set; }
    }

    /// <summary>
    /// Budget alert for virtual key.
    /// </summary>
    public class BudgetAlert
    {
        public int VirtualKeyId { get; set; }
        public string AlertType { get; set; } = string.Empty;
        public double BudgetUtilization { get; set; }
        public DateTime ProjectedExhaustion { get; set; }
    }

    /// <summary>
    /// Anomaly detection report.
    /// </summary>
    public class AnomalyDetectionReport
    {
        public DateTime GeneratedAt { get; set; }
        public List<PerformanceAnomaly> Anomalies { get; set; } = new();
        public Dictionary<string, AnomalyPattern> Patterns { get; set; } = new();
        public List<string> AffectedProviders { get; set; } = new();
        public AnomalySummary Summary { get; set; } = new();
    }

    /// <summary>
    /// Performance anomaly detection.
    /// </summary>
    public class PerformanceAnomaly
    {
        public DateTime DetectedAt { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double AnomalyScore { get; set; }
        public Dictionary<string, object> Metrics { get; set; } = new();
        public string? PossibleCause { get; set; }
    }

    /// <summary>
    /// Anomaly pattern.
    /// </summary>
    public class AnomalyPattern
    {
        public string PatternType { get; set; } = string.Empty;
        public int Occurrences { get; set; }
        public TimeSpan AverageDuration { get; set; }
        public double RecurrenceProbability { get; set; }
    }

    /// <summary>
    /// Anomaly summary.
    /// </summary>
    public class AnomalySummary
    {
        public int TotalAnomalies { get; set; }
        public int CriticalAnomalies { get; set; }
        public double SystemStability { get; set; }
        public List<string> RecommendedActions { get; set; } = new();
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
}