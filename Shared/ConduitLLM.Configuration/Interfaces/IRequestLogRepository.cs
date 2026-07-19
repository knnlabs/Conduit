using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Interfaces
{
    /// <summary>
    /// Repository interface for managing request logs.
    /// Extends IRepositoryBase for standard CRUD operations.
    /// </summary>
    public interface IRequestLogRepository : IRepositoryBase<RequestLog, int>
    {
        /// <summary>
        /// Gets request logs for a specific virtual key
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of request logs for the specified virtual key</returns>
        /// <remarks>This method is obsolete. Use GetByVirtualKeyIdPaginatedAsync instead for better performance.</remarks>
        [Obsolete("Use GetByVirtualKeyIdPaginatedAsync instead. This method loads all records into memory and will be removed in a future version.")]
        Task<List<RequestLog>> GetByVirtualKeyIdAsync(int virtualKeyId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets paginated request logs for a specific virtual key
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID</param>
        /// <param name="pageNumber">The page number (1-based)</param>
        /// <param name="pageSize">The page size</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A paginated list of request logs for the specified virtual key</returns>
        Task<(List<RequestLog> Logs, int TotalCount)> GetByVirtualKeyIdPaginatedAsync(
            int virtualKeyId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets request logs for a specific date range.
        /// WARNING: Loads all matching rows into memory. Prefer aggregate methods
        /// (GetCostsByDateAsync, GetAggregatedByModelAsync, GetSummaryAsync, etc.)
        /// for analytics queries, or GetByDateRangePaginatedAsync for browsing.
        /// </summary>
        /// <param name="startDate">The start date</param>
        /// <param name="endDate">The end date</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of request logs within the specified date range</returns>
        Task<List<RequestLog>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets request logs for a date range with optional model and virtual key filters
        /// applied at the database level. Used by the analytics export endpoint to avoid
        /// loading every row in the range when filters are present.
        /// </summary>
        /// <param name="startDate">The start date.</param>
        /// <param name="endDate">The end date.</param>
        /// <param name="modelFilter">Optional case-insensitive substring match against ModelName.</param>
        /// <param name="virtualKeyId">Optional virtual key ID to filter on.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<List<RequestLog>> GetByDateRangeFilteredAsync(
            DateTime startDate,
            DateTime endDate,
            string? modelFilter = null,
            int? virtualKeyId = null,
            CancellationToken cancellationToken = default);

        #region Database-Level Aggregation Methods

        /// <summary>
        /// Gets costs aggregated by date within a date range, computed at the database level.
        /// Returns one row per day instead of loading all individual request logs.
        /// </summary>
        Task<List<DateCostAggregation>> GetCostsByDateAsync(
            DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets request log data aggregated by model within a date range, computed at the database level.
        /// </summary>
        Task<List<ModelAggregation>> GetAggregatedByModelAsync(
            DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets request log data aggregated by model for a specific virtual key, computed at the database level.
        /// </summary>
        Task<List<ModelAggregation>> GetAggregatedByModelForVirtualKeyAsync(
            int virtualKeyId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets request log data aggregated by virtual key within a date range, computed at the database level.
        /// </summary>
        Task<List<VirtualKeyAggregation>> GetAggregatedByVirtualKeyAsync(
            DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets summary statistics (totals) for a date range in a single database query.
        /// Returns one row with aggregate counts, sums, and averages.
        /// </summary>
        Task<RequestLogSummary> GetSummaryAsync(
            DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets summary statistics for a specific virtual key and date range in a single database query.
        /// </summary>
        Task<RequestLogSummary> GetSummaryForVirtualKeyAsync(
            int virtualKeyId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets daily statistics (per-day breakdown) within a date range, computed at the database level.
        /// Can be further aggregated to weekly/monthly in C# with minimal overhead (~365 rows/year).
        /// </summary>
        Task<List<DailyStatisticsAggregation>> GetDailyStatisticsAsync(
            DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        #endregion

        /// <summary>
        /// Gets paginated request logs for a specific date range
        /// </summary>
        /// <param name="startDate">The start date</param>
        /// <param name="endDate">The end date</param>
        /// <param name="pageNumber">The page number (1-based)</param>
        /// <param name="pageSize">The page size</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A paginated list of request logs within the specified date range</returns>
        Task<(List<RequestLog> Logs, int TotalCount)> GetByDateRangePaginatedAsync(
            DateTime startDate,
            DateTime endDate,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets paginated request logs for a specific model
        /// </summary>
        /// <param name="modelName">The model name</param>
        /// <param name="pageNumber">The page number (1-based)</param>
        /// <param name="pageSize">The page size</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A paginated list of request logs for the specified model</returns>
        Task<(List<RequestLog> Logs, int TotalCount)> GetByModelPaginatedAsync(
            string modelName,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets distinct model names from request logs
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of distinct model names used in request logs</returns>
        Task<List<string>> GetDistinctModelsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets usage statistics
        /// </summary>
        /// <param name="startDate">The start date</param>
        /// <param name="endDate">The end date</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Usage statistics for the specified date range</returns>
        Task<UsageStatisticsDto> GetUsageStatisticsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the cost and metadata of a request log by task ID.
        /// Used to correct async video/image request logs after generation completes.
        /// </summary>
        /// <param name="taskId">The task ID stored in the metadata</param>
        /// <param name="cost">The actual cost to set</param>
        /// <param name="modelName">The model name to set (if different from original)</param>
        /// <param name="durationSeconds">The actual duration in seconds (for video)</param>
        /// <param name="resolution">The actual resolution (for video/image)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the update was successful, false if the request log was not found</returns>
        Task<bool> UpdateCostByTaskIdAsync(
            string taskId,
            decimal cost,
            string? modelName = null,
            double? durationSeconds = null,
            string? resolution = null,
            CancellationToken cancellationToken = default);
    }
}
