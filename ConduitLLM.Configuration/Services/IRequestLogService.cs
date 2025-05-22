using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Services.Dtos; 

namespace ConduitLLM.Configuration.Services
{
    /// <summary>
    /// Service for logging and retrieving API requests made using virtual keys
    /// </summary>
    public interface IRequestLogService
    {
        /// <summary>
        /// Logs a request made with a virtual key
        /// </summary>
        /// <param name="request">Request details to log</param>
        Task LogRequestAsync(LogRequestDto request);

        /// <summary>
        /// Gets virtual key ID from the key hash
        /// </summary>
        /// <param name="keyValue">The virtual key hash</param>
        /// <returns>The ID of the virtual key, or null if not found</returns>
        Task<int?> GetVirtualKeyIdFromKeyValueAsync(string keyValue);

        /// <summary>
        /// Estimates token counts for request and response
        /// </summary>
        /// <param name="requestContent">The request content</param>
        /// <param name="responseContent">The response content</param>
        /// <returns>Tuple of (inputTokens, outputTokens)</returns>
        (int InputTokens, int OutputTokens) EstimateTokens(string requestContent, string responseContent);

        /// <summary>
        /// Calculates the cost of a request based on model and token counts
        /// </summary>
        /// <param name="modelName">Name of the model used</param>
        /// <param name="inputTokens">Number of input tokens</param>
        /// <param name="outputTokens">Number of output tokens</param>
        /// <returns>The calculated cost</returns>
        decimal CalculateCost(string modelName, int inputTokens, int outputTokens);

        /// <summary>
        /// Gets usage statistics for a virtual key for a specified time period
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID</param>
        /// <param name="startDate">Start date of the period</param>
        /// <param name="endDate">End date of the period</param>
        /// <returns>Usage statistics</returns>
        Task<UsageStatisticsDto> GetUsageStatisticsAsync(int virtualKeyId, DateTime startDate, DateTime endDate);
        
        /// <summary>
        /// Gets paged request logs for a virtual key
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID</param>
        /// <param name="pageNumber">Page number (1-based)</param>
        /// <param name="pageSize">Page size</param>
        /// <returns>Paged list of request logs</returns>
        Task<(List<RequestLog> Logs, int TotalCount)> GetPagedRequestLogsAsync(
            int virtualKeyId,
            int pageNumber = 1,
            int pageSize = 20);
            
        /// <summary>
        /// Searches for request logs with various filter criteria
        /// </summary>
        /// <param name="virtualKeyId">Optional ID of the virtual key to filter by</param>
        /// <param name="modelFilter">Optional model name to filter by</param>
        /// <param name="startDate">Start date of the period</param>
        /// <param name="endDate">End date of the period</param>
        /// <param name="statusCode">Optional status code to filter by</param>
        /// <param name="pageNumber">Page number (1-based)</param>
        /// <param name="pageSize">Page size</param>
        /// <returns>Paged list of request logs and total count</returns>
        Task<(List<RequestLog> Logs, int TotalCount)> SearchLogsAsync(
            int? virtualKeyId,
            string? modelFilter,
            DateTime startDate,
            DateTime endDate,
            int? statusCode,
            int pageNumber = 1,
            int pageSize = 20);
            
        /// <summary>
        /// Gets summary statistics for request logs in a specified time period
        /// </summary>
        /// <param name="startDate">Start date of the period</param>
        /// <param name="endDate">End date of the period</param>
        /// <returns>Summary statistics</returns>
        Task<DTOs.LogsSummaryDto> GetLogsSummaryAsync(DateTime startDate, DateTime endDate);
        
        /// <summary>
        /// Gets all distinct model names from the request logs
        /// </summary>
        /// <returns>List of distinct model names</returns>
        Task<List<string>> GetDistinctModelsAsync();
    }
}
