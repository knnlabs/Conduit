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
    public partial interface IImageGenerationAnalyticsService
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
    /// Trend direction for image generation metrics.
    /// </summary>
    public enum ImageGenerationTrendDirection
    {
        Declining,
        Stable,
        Growing,
        Volatile
    }
}