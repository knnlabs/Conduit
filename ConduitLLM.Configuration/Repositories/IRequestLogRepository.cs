using System;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Repositories
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
        Task<List<RequestLog>> GetAllAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets request logs for a specific virtual key
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of request logs for the specified virtual key</returns>
        Task<List<RequestLog>> GetByVirtualKeyIdAsync(int virtualKeyId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets request logs for a specific date range
        /// </summary>
        /// <param name="startDate">The start date</param>
        /// <param name="endDate">The end date</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of request logs within the specified date range</returns>
        Task<List<RequestLog>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets request logs for a specific model
        /// </summary>
        /// <param name="modelName">The model name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of request logs for the specified model</returns>
        Task<List<RequestLog>> GetByModelAsync(string modelName, CancellationToken cancellationToken = default);
        
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
    }
}