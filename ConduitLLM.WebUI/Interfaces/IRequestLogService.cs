using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Services.Dtos;
using ConduitLLM.WebUI.DTOs;

namespace ConduitLLM.WebUI.Interfaces
{
    /// <summary>
    /// Interface for logging API requests made with virtual keys using the repository pattern
    /// </summary>
    public interface IRequestLogService
    {
        /// <summary>
        /// Creates a new request log entry
        /// </summary>
        Task<RequestLog> CreateRequestLogAsync(
            int virtualKeyId,
            string modelName,
            string requestType,
            int inputTokens,
            int outputTokens,
            decimal cost,
            double responseTimeMs,
            string? userId = null,
            string? clientIp = null,
            string? requestPath = null,
            int? statusCode = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets request logs for a specific virtual key
        /// </summary>
        Task<(List<RequestLog> Logs, int TotalCount)> GetRequestLogsForKeyAsync(
            int virtualKeyId, 
            int page = 1, 
            int pageSize = 100,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets summary statistics for a specific virtual key
        /// </summary>
        Task<KeyUsageSummary?> GetKeyUsageSummaryAsync(
            int virtualKeyId, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets aggregated usage data for all virtual keys in the system
        /// </summary>
        Task<List<KeyAggregateSummary>?> GetAllKeysUsageSummaryAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets daily usage statistics for a specific period
        /// </summary>
        Task<List<DailyUsageSummary>?> GetDailyUsageStatsAsync(
            int? virtualKeyId = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Searches for request logs with various filter criteria
        /// </summary>
        Task<(List<RequestLog> Logs, int TotalCount)> SearchLogsAsync(
            int? virtualKeyId,
            string? modelFilter,
            DateTime startDate,
            DateTime endDate,
            int? statusCode,
            int pageNumber = 1,
            int pageSize = 20,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a summary of logs for the specified date range
        /// </summary>
        Task<LogsSummaryDto> GetLogsSummaryAsync(
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all distinct model names from the request logs
        /// </summary>
        Task<List<string>> GetDistinctModelsAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts a timer for tracking request execution time
        /// </summary>
        Stopwatch StartRequestTimer();
    }
}