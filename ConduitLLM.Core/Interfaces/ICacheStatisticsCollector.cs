using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Collects and manages detailed statistics for cache operations.
    /// </summary>
    public interface ICacheStatisticsCollector
    {
        /// <summary>
        /// Records a cache operation.
        /// </summary>
        /// <param name="operation">The cache operation details.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task RecordOperationAsync(CacheOperation operation, CancellationToken cancellationToken = default);

        /// <summary>
        /// Records multiple cache operations in batch.
        /// </summary>
        /// <param name="operations">The cache operations to record.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task RecordOperationBatchAsync(IEnumerable<CacheOperation> operations, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets current statistics for a specific cache region.
        /// </summary>
        /// <param name="region">The cache region.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Statistics for the region.</returns>
        Task<CacheStatistics> GetStatisticsAsync(CacheRegion region, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets current statistics for all cache regions.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Statistics for all regions.</returns>
        Task<Dictionary<CacheRegion, CacheStatistics>> GetAllStatisticsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets statistics for a specific time window.
        /// </summary>
        /// <param name="region">The cache region.</param>
        /// <param name="window">The time window.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Statistics for the time window.</returns>
        Task<CacheStatistics> GetStatisticsForWindowAsync(
            CacheRegion region, 
            TimeWindow window, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets historical statistics over a time range.
        /// </summary>
        /// <param name="region">The cache region.</param>
        /// <param name="startTime">Start time of the range.</param>
        /// <param name="endTime">End time of the range.</param>
        /// <param name="interval">Interval for data points.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Time-series statistics data.</returns>
        Task<IEnumerable<TimeSeriesStatistics>> GetHistoricalStatisticsAsync(
            CacheRegion region,
            DateTime startTime,
            DateTime endTime,
            TimeSpan interval,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Resets statistics for a specific region.
        /// </summary>
        /// <param name="region">The cache region.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ResetStatisticsAsync(CacheRegion region, CancellationToken cancellationToken = default);

        /// <summary>
        /// Resets all statistics.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ResetAllStatisticsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Exports statistics in a format suitable for monitoring systems.
        /// </summary>
        /// <param name="format">Export format (e.g., "prometheus", "json").</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Exported statistics data.</returns>
        Task<string> ExportStatisticsAsync(string format, CancellationToken cancellationToken = default);

        /// <summary>
        /// Configures alert thresholds for statistics.
        /// </summary>
        /// <param name="region">The cache region.</param>
        /// <param name="thresholds">Alert thresholds configuration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ConfigureAlertsAsync(
            CacheRegion region, 
            CacheAlertThresholds thresholds, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets current alerts based on configured thresholds.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Active cache alerts.</returns>
        Task<IEnumerable<CacheAlert>> GetActiveAlertsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Event raised when statistics are updated.
        /// </summary>
        event EventHandler<CacheStatisticsUpdatedEventArgs>? StatisticsUpdated;

        /// <summary>
        /// Event raised when an alert threshold is exceeded.
        /// </summary>
        event EventHandler<CacheAlertEventArgs>? AlertTriggered;
    }

    /// <summary>
    /// Time windows for statistics aggregation.
    /// </summary>
    public enum TimeWindow
    {
        /// <summary>
        /// Last minute.
        /// </summary>
        LastMinute,

        /// <summary>
        /// Last 5 minutes.
        /// </summary>
        Last5Minutes,

        /// <summary>
        /// Last 15 minutes.
        /// </summary>
        Last15Minutes,

        /// <summary>
        /// Last hour.
        /// </summary>
        LastHour,

        /// <summary>
        /// Last 24 hours.
        /// </summary>
        Last24Hours,

        /// <summary>
        /// Last 7 days.
        /// </summary>
        Last7Days,

        /// <summary>
        /// Last 30 days.
        /// </summary>
        Last30Days,

        /// <summary>
        /// All time.
        /// </summary>
        AllTime
    }
}