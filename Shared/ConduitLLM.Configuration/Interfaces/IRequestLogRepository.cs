using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Interfaces
{
    /// <summary>
    /// Repository interface for managing request logs
    /// </summary>
    public interface IRequestLogRepository
    {
        /// <summary>
        /// Gets a request log by ID
        /// </summary>
        /// <param name="id">The request log ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The request log entity or null if not found</returns>
        Task<RequestLog?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all request logs
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of all request logs</returns>
        /// <remarks>This method is obsolete. Use GetPaginatedAsync instead for better performance.</remarks>
        [Obsolete("Use GetPaginatedAsync instead. This method loads all records into memory and will be removed in a future version.")]
        Task<List<RequestLog>> GetAllAsync(CancellationToken cancellationToken = default);

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
        /// Gets request logs for a specific date range
        /// </summary>
        /// <param name="startDate">The start date</param>
        /// <param name="endDate">The end date</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of request logs within the specified date range</returns>
        Task<List<RequestLog>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

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
        /// Gets request logs for a specific model
        /// </summary>
        /// <param name="modelName">The model name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of request logs for the specified model</returns>
        /// <remarks>This method is obsolete. Use GetByModelPaginatedAsync instead for better performance.</remarks>
        [Obsolete("Use GetByModelPaginatedAsync instead. This method loads all records into memory and will be removed in a future version.")]
        Task<List<RequestLog>> GetByModelAsync(string modelName, CancellationToken cancellationToken = default);

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
        /// Gets paginated request logs
        /// </summary>
        /// <param name="pageNumber">The page number (1-based)</param>
        /// <param name="pageSize">The page size</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A paginated list of request logs</returns>
        Task<(List<RequestLog> Logs, int TotalCount)> GetPaginatedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new request log
        /// </summary>
        /// <param name="requestLog">The request log to create</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The ID of the created request log</returns>
        Task<int> CreateAsync(RequestLog requestLog, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates a request log
        /// </summary>
        /// <param name="requestLog">The request log to update</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the update was successful, false otherwise</returns>
        Task<bool> UpdateAsync(RequestLog requestLog, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a request log
        /// </summary>
        /// <param name="id">The ID of the request log to delete</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the deletion was successful, false otherwise</returns>
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

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
