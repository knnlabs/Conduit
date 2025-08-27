using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Abstraction for Redis operations related to error tracking
    /// </summary>
    public interface IRedisErrorStore
    {
        /// <summary>
        /// Track a fatal error for a key
        /// </summary>
        /// <param name="keyId">The key credential ID</param>
        /// <param name="error">The error information</param>
        Task TrackFatalErrorAsync(int keyId, ProviderErrorInfo error);

        /// <summary>
        /// Track a warning for a key
        /// </summary>
        /// <param name="keyId">The key credential ID</param>
        /// <param name="error">The error information</param>
        Task TrackWarningAsync(int keyId, ProviderErrorInfo error);

        /// <summary>
        /// Update provider summary statistics
        /// </summary>
        /// <param name="providerId">The provider ID</param>
        /// <param name="isFatal">Whether this was a fatal error</param>
        Task UpdateProviderSummaryAsync(int providerId, bool isFatal);

        /// <summary>
        /// Add an error to the global feed
        /// </summary>
        /// <param name="error">The error information</param>
        Task AddToGlobalFeedAsync(ProviderErrorInfo error);

        /// <summary>
        /// Get fatal error information for a key
        /// </summary>
        /// <param name="keyId">The key credential ID</param>
        /// <returns>Fatal error data or null if not found</returns>
        Task<FatalErrorData?> GetFatalErrorDataAsync(int keyId);

        /// <summary>
        /// Mark a key as disabled in Redis
        /// </summary>
        /// <param name="keyId">The key credential ID</param>
        /// <param name="disabledAt">When the key was disabled</param>
        Task MarkKeyDisabledAsync(int keyId, DateTime disabledAt);

        /// <summary>
        /// Update provider disabled status
        /// </summary>
        /// <param name="providerId">The provider ID</param>
        /// <param name="disabledAt">When the provider was disabled</param>
        /// <param name="reason">The reason for disabling</param>
        Task MarkProviderDisabledAsync(int providerId, DateTime disabledAt, string reason);

        /// <summary>
        /// Add a key to the provider's disabled keys list
        /// </summary>
        /// <param name="providerId">The provider ID</param>
        /// <param name="keyId">The key credential ID</param>
        Task AddDisabledKeyToProviderAsync(int providerId, int keyId);

        /// <summary>
        /// Get recent errors from the feed
        /// </summary>
        /// <param name="limit">Maximum number of errors to return</param>
        /// <returns>List of recent errors</returns>
        Task<IReadOnlyList<ErrorFeedEntry>> GetRecentErrorsAsync(int limit = 100);

        /// <summary>
        /// Get error counts for keys of a provider
        /// </summary>
        /// <param name="providerId">The provider ID</param>
        /// <param name="keyIds">The key IDs to check</param>
        /// <param name="window">Time window to check</param>
        /// <returns>Dictionary of key ID to error count</returns>
        Task<Dictionary<int, ErrorCountInfo>> GetErrorCountsByKeysAsync(int providerId, IEnumerable<int> keyIds, TimeSpan window);

        /// <summary>
        /// Clear all error data for a key
        /// </summary>
        /// <param name="keyId">The key credential ID</param>
        Task ClearErrorsForKeyAsync(int keyId);

        /// <summary>
        /// Get all error data for a key
        /// </summary>
        /// <param name="keyId">The key credential ID</param>
        /// <returns>Complete error data for the key</returns>
        Task<KeyErrorData?> GetKeyErrorDataAsync(int keyId);

        /// <summary>
        /// Get provider error summary
        /// </summary>
        /// <param name="providerId">The provider ID</param>
        /// <returns>Provider summary data</returns>
        Task<ProviderSummaryData?> GetProviderSummaryAsync(int providerId);

        /// <summary>
        /// Get error statistics for a time window
        /// </summary>
        /// <param name="window">Time window to analyze</param>
        /// <returns>Statistics for the time period</returns>
        Task<ErrorStatsData> GetErrorStatisticsAsync(TimeSpan window);
    }

    /// <summary>
    /// Fatal error data from Redis
    /// </summary>
    public class FatalErrorData
    {
        public string? ErrorType { get; set; }
        public int Count { get; set; }
        public DateTime? FirstSeen { get; set; }
        public DateTime? LastSeen { get; set; }
        public string? LastErrorMessage { get; set; }
        public int? LastStatusCode { get; set; }
        public DateTime? DisabledAt { get; set; }
    }

    /// <summary>
    /// Error feed entry
    /// </summary>
    public class ErrorFeedEntry
    {
        public int KeyId { get; set; }
        public int ProviderId { get; set; }
        public string ErrorType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Error count information for a key
    /// </summary>
    public class ErrorCountInfo
    {
        public int Count { get; set; }
        public DateTime LastSeen { get; set; }
    }

    /// <summary>
    /// Complete error data for a key
    /// </summary>
    public class KeyErrorData
    {
        public FatalErrorData? FatalError { get; set; }
        public List<WarningData> RecentWarnings { get; set; } = new();
    }

    /// <summary>
    /// Warning data
    /// </summary>
    public class WarningData
    {
        public string Type { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Provider summary data from Redis
    /// </summary>
    public class ProviderSummaryData
    {
        public int TotalErrors { get; set; }
        public int FatalErrors { get; set; }
        public int Warnings { get; set; }
        public List<int> DisabledKeyIds { get; set; } = new();
        public DateTime? LastError { get; set; }
        public DateTime? ProviderDisabledAt { get; set; }
        public string? ProviderDisableReason { get; set; }
    }

    /// <summary>
    /// Error statistics data
    /// </summary>
    public class ErrorStatsData
    {
        public int TotalErrors { get; set; }
        public int FatalErrors { get; set; }
        public int Warnings { get; set; }
        public Dictionary<string, int> ErrorsByType { get; set; } = new();
    }
}