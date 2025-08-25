using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Service for tracking and managing provider API errors
    /// </summary>
    public interface IProviderErrorTrackingService
    {
        /// <summary>
        /// Track a provider error occurrence
        /// </summary>
        /// <param name="error">Error information to track</param>
        Task TrackErrorAsync(ProviderErrorInfo error);
        
        /// <summary>
        /// Determine if a key should be disabled based on error history
        /// </summary>
        /// <param name="keyId">ID of the key credential</param>
        /// <param name="errorType">Type of error that occurred</param>
        /// <returns>True if the key should be disabled</returns>
        Task<bool> ShouldDisableKeyAsync(int keyId, ProviderErrorType errorType);
        
        /// <summary>
        /// Disable a provider key due to errors
        /// </summary>
        /// <param name="keyId">ID of the key to disable</param>
        /// <param name="reason">Reason for disabling</param>
        Task DisableKeyAsync(int keyId, string reason);
        
        /// <summary>
        /// Get recent errors for monitoring
        /// </summary>
        /// <param name="providerId">Optional provider ID filter</param>
        /// <param name="keyId">Optional key ID filter</param>
        /// <param name="limit">Maximum number of errors to return</param>
        /// <returns>List of recent errors</returns>
        Task<IReadOnlyList<ProviderErrorInfo>> GetRecentErrorsAsync(
            int? providerId = null, 
            int? keyId = null,
            int limit = 100);
        
        /// <summary>
        /// Get error counts by key for a specific time window
        /// </summary>
        /// <param name="providerId">Provider ID</param>
        /// <param name="window">Time window to check</param>
        /// <returns>Dictionary of key ID to error count</returns>
        Task<Dictionary<int, int>> GetErrorCountsByKeyAsync(int providerId, TimeSpan window);
        
        /// <summary>
        /// Clear all errors for a key (used when re-enabling)
        /// </summary>
        /// <param name="keyId">ID of the key to clear errors for</param>
        Task ClearErrorsForKeyAsync(int keyId);
        
        /// <summary>
        /// Get detailed error information for a specific key
        /// </summary>
        /// <param name="keyId">ID of the key</param>
        /// <returns>Detailed error information or null if not found</returns>
        Task<KeyErrorDetails?> GetKeyErrorDetailsAsync(int keyId);
        
        /// <summary>
        /// Get provider-level error summary
        /// </summary>
        /// <param name="providerId">Provider ID</param>
        /// <returns>Summary of errors for the provider</returns>
        Task<ProviderErrorSummary?> GetProviderSummaryAsync(int providerId);
        
        /// <summary>
        /// Get error statistics for dashboard
        /// </summary>
        /// <param name="window">Time window for statistics</param>
        /// <returns>Error statistics</returns>
        Task<ErrorStatistics> GetErrorStatisticsAsync(TimeSpan window);
    }
    
    /// <summary>
    /// Detailed error information for a specific key
    /// </summary>
    public class KeyErrorDetails
    {
        public int KeyId { get; set; }
        public string KeyName { get; set; } = string.Empty;
        public bool IsDisabled { get; set; }
        public DateTime? DisabledAt { get; set; }
        public FatalErrorInfo? FatalError { get; set; }
        public List<WarningInfo> RecentWarnings { get; set; } = new();
    }
    
    /// <summary>
    /// Information about a fatal error
    /// </summary>
    public class FatalErrorInfo
    {
        public ProviderErrorType ErrorType { get; set; }
        public int Count { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public string LastErrorMessage { get; set; } = string.Empty;
        public int? LastStatusCode { get; set; }
    }
    
    /// <summary>
    /// Information about a warning
    /// </summary>
    public class WarningInfo
    {
        public ProviderErrorType Type { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
    
    /// <summary>
    /// Provider-level error summary
    /// </summary>
    public class ProviderErrorSummary
    {
        public int ProviderId { get; set; }
        public string ProviderName { get; set; } = string.Empty;
        public int TotalErrors { get; set; }
        public int FatalErrors { get; set; }
        public int Warnings { get; set; }
        public List<int> DisabledKeyIds { get; set; } = new();
        public DateTime? LastError { get; set; }
    }
    
    /// <summary>
    /// Error statistics for dashboard
    /// </summary>
    public class ErrorStatistics
    {
        public int TotalErrors { get; set; }
        public int FatalErrors { get; set; }
        public int Warnings { get; set; }
        public int DisabledKeys { get; set; }
        public Dictionary<string, int> ErrorsByType { get; set; } = new();
        public Dictionary<string, int> ErrorsByProvider { get; set; } = new();
        public List<HourlyErrorCount> HourlyTrend { get; set; } = new();
    }
    
    /// <summary>
    /// Hourly error count for trending
    /// </summary>
    public class HourlyErrorCount
    {
        public DateTime Hour { get; set; }
        public int Count { get; set; }
    }
}