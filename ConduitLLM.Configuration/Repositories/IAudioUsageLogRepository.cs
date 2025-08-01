using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.Audio;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository interface for audio usage logging.
    /// </summary>
    public interface IAudioUsageLogRepository
    {
        /// <summary>
        /// Creates a new audio usage log entry.
        /// </summary>
        Task<AudioUsageLog> CreateAsync(AudioUsageLog log);

        /// <summary>
        /// Gets audio usage logs with pagination.
        /// </summary>
        Task<PagedResult<AudioUsageLog>> GetPagedAsync(AudioUsageQueryDto query);

        /// <summary>
        /// Gets usage summary statistics.
        /// </summary>
        Task<AudioUsageSummaryDto> GetUsageSummaryAsync(DateTime startDate, DateTime endDate, string? virtualKey = null, int? providerId = null);

        /// <summary>
        /// Gets usage by virtual key.
        /// </summary>
        Task<List<AudioUsageLog>> GetByVirtualKeyAsync(string virtualKey, DateTime? startDate = null, DateTime? endDate = null);

        /// <summary>
        /// Gets usage by provider.
        /// </summary>
        Task<List<AudioUsageLog>> GetByProviderAsync(int providerId, DateTime? startDate = null, DateTime? endDate = null);

        /// <summary>
        /// Gets usage by session ID.
        /// </summary>
        Task<List<AudioUsageLog>> GetBySessionIdAsync(string sessionId);

        /// <summary>
        /// Gets total cost for a virtual key within a date range.
        /// </summary>
        Task<decimal> GetTotalCostAsync(string virtualKey, DateTime startDate, DateTime endDate);

        /// <summary>
        /// Gets operation type breakdown for analytics.
        /// </summary>
        Task<List<OperationTypeBreakdown>> GetOperationBreakdownAsync(DateTime startDate, DateTime endDate, string? virtualKey = null);

        /// <summary>
        /// Gets provider breakdown for analytics.
        /// </summary>
        Task<List<ProviderBreakdown>> GetProviderBreakdownAsync(DateTime startDate, DateTime endDate, string? virtualKey = null);

        /// <summary>
        /// Gets virtual key breakdown for analytics.
        /// </summary>
        Task<List<VirtualKeyBreakdown>> GetVirtualKeyBreakdownAsync(DateTime startDate, DateTime endDate, int? providerId = null);

        /// <summary>
        /// Deletes old usage logs based on retention policy.
        /// </summary>
        Task<int> DeleteOldLogsAsync(DateTime cutoffDate);
    }
}
