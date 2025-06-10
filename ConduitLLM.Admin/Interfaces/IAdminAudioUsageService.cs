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
        Task<AudioUsageSummaryDto> GetUsageSummaryAsync(DateTime startDate, DateTime endDate, string? virtualKey = null, string? provider = null);

        /// <summary>
        /// Gets audio usage by virtual key.
        /// </summary>
        Task<AudioKeyUsageDto> GetUsageByKeyAsync(string virtualKey, DateTime? startDate = null, DateTime? endDate = null);

        /// <summary>
        /// Gets audio usage by provider.
        /// </summary>
        Task<AudioProviderUsageDto> GetUsageByProviderAsync(string provider, DateTime? startDate = null, DateTime? endDate = null);

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

    /// <summary>
    /// Audio usage data for a specific virtual key.
    /// </summary>
    public class AudioKeyUsageDto
    {
        /// <summary>
        /// Virtual key hash.
        /// </summary>
        public string VirtualKey { get; set; } = string.Empty;

        /// <summary>
        /// Key name if available.
        /// </summary>
        public string? KeyName { get; set; }

        /// <summary>
        /// Total operations count.
        /// </summary>
        public int TotalOperations { get; set; }

        /// <summary>
        /// Total cost incurred.
        /// </summary>
        public decimal TotalCost { get; set; }

        /// <summary>
        /// Usage breakdown by operation type.
        /// </summary>
        public List<OperationTypeBreakdown> OperationBreakdown { get; set; } = new();

        /// <summary>
        /// Usage breakdown by provider.
        /// </summary>
        public List<ProviderBreakdown> ProviderBreakdown { get; set; } = new();

        /// <summary>
        /// Recent usage logs.
        /// </summary>
        public List<AudioUsageDto> RecentLogs { get; set; } = new();
    }

    /// <summary>
    /// Audio usage data for a specific provider.
    /// </summary>
    public class AudioProviderUsageDto
    {
        /// <summary>
        /// Provider name.
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Total operations count.
        /// </summary>
        public int TotalOperations { get; set; }

        /// <summary>
        /// Success rate percentage.
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// Average response time in milliseconds.
        /// </summary>
        public double AverageResponseTime { get; set; }

        /// <summary>
        /// Total cost incurred.
        /// </summary>
        public decimal TotalCost { get; set; }

        /// <summary>
        /// Usage breakdown by operation type.
        /// </summary>
        public List<OperationTypeBreakdown> OperationBreakdown { get; set; } = new();

        /// <summary>
        /// Daily usage trend.
        /// </summary>
        public List<DailyUsageTrend> DailyTrend { get; set; } = new();
    }

    /// <summary>
    /// Daily usage trend data.
    /// </summary>
    public class DailyUsageTrend
    {
        /// <summary>
        /// Date of the usage.
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Number of operations.
        /// </summary>
        public int Operations { get; set; }

        /// <summary>
        /// Total cost for the day.
        /// </summary>
        public decimal Cost { get; set; }
    }
}