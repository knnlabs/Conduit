using System;
using System.Threading.Tasks;

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
    }
}
