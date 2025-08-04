using System;
using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.Audio;

namespace ConduitLLM.Admin.Interfaces
{
    /// <summary>
    /// Service interface for managing audio usage analytics.
    /// </summary>
    public interface IAdminAudioUsageService
    {
        /// <summary>
        /// Gets paginated audio usage logs.
        /// </summary>
        Task<PagedResult<AudioUsageDto>> GetUsageLogsAsync(AudioUsageQueryDto query);

        /// <summary>
        /// Gets audio usage summary statistics.
        /// </summary>
        Task<AudioUsageSummaryDto> GetUsageSummaryAsync(DateTime startDate, DateTime endDate, string? virtualKey = null, int? providerId = null);

        /// <summary>
        /// Gets audio usage by virtual key.
        /// </summary>
        Task<AudioKeyUsageDto> GetUsageByKeyAsync(string virtualKey, DateTime? startDate = null, DateTime? endDate = null);

        /// <summary>
        /// Gets audio usage by provider.
        /// </summary>
        Task<AudioProviderUsageDto> GetUsageByProviderAsync(int providerId, DateTime? startDate = null, DateTime? endDate = null);

        /// <summary>
        /// Gets real-time session metrics.
        /// </summary>
        Task<RealtimeSessionMetricsDto> GetRealtimeSessionMetricsAsync();

        /// <summary>
        /// Gets active real-time sessions.
        /// </summary>
        Task<List<RealtimeSessionDto>> GetActiveSessionsAsync();

        /// <summary>
        /// Gets details of a specific real-time session.
        /// </summary>
        Task<RealtimeSessionDto?> GetSessionDetailsAsync(string sessionId);

        /// <summary>
        /// Terminates an active real-time session.
        /// </summary>
        Task<bool> TerminateSessionAsync(string sessionId);

        /// <summary>
        /// Exports usage data to CSV or JSON format.
        /// </summary>
        Task<string> ExportUsageDataAsync(AudioUsageQueryDto query, string format);

        /// <summary>
        /// Cleans up old usage logs based on retention policy.
        /// </summary>
        Task<int> CleanupOldLogsAsync(int retentionDays);
    }
}
