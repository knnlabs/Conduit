using System;
using System.Collections.Generic;

namespace ConduitLLM.Core.Interfaces
{
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
}