using System;
using System.Collections.Generic;

namespace ConduitLLM.Admin.DTOs
{
    /// <summary>
    /// DTO for individual provider error occurrence
    /// </summary>
    public class ProviderErrorDto
    {
        /// <summary>
        /// ID of the key credential that caused the error
        /// </summary>
        public int KeyCredentialId { get; set; }
        
        /// <summary>
        /// Name of the key for display
        /// </summary>
        public string? KeyName { get; set; }
        
        /// <summary>
        /// ID of the provider
        /// </summary>
        public int ProviderId { get; set; }
        
        /// <summary>
        /// Name of the provider
        /// </summary>
        public string? ProviderName { get; set; }
        
        /// <summary>
        /// Type of error
        /// </summary>
        public string ErrorType { get; set; } = string.Empty;
        
        /// <summary>
        /// Error message
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;
        
        /// <summary>
        /// HTTP status code if applicable
        /// </summary>
        public int? HttpStatusCode { get; set; }
        
        /// <summary>
        /// When the error occurred
        /// </summary>
        public DateTime OccurredAt { get; set; }
        
        /// <summary>
        /// Whether this is a fatal error
        /// </summary>
        public bool IsFatal { get; set; }
        
        /// <summary>
        /// Model that was being used
        /// </summary>
        public string? ModelName { get; set; }
    }

    /// <summary>
    /// DTO for provider-level error summary
    /// </summary>
    public class ProviderErrorSummaryDto
    {
        /// <summary>
        /// Provider ID
        /// </summary>
        public int ProviderId { get; set; }
        
        /// <summary>
        /// Provider name
        /// </summary>
        public string ProviderName { get; set; } = string.Empty;
        
        /// <summary>
        /// Total number of errors
        /// </summary>
        public int TotalErrors { get; set; }
        
        /// <summary>
        /// Number of fatal errors
        /// </summary>
        public int FatalErrors { get; set; }
        
        /// <summary>
        /// Number of warnings
        /// </summary>
        public int Warnings { get; set; }
        
        /// <summary>
        /// List of disabled key IDs
        /// </summary>
        public List<int> DisabledKeyIds { get; set; } = new();
        
        /// <summary>
        /// When the last error occurred
        /// </summary>
        public DateTime? LastError { get; set; }
        
        /// <summary>
        /// Number of currently disabled keys
        /// </summary>
        public int DisabledKeyCount => DisabledKeyIds.Count;
    }

    /// <summary>
    /// DTO for detailed key error information
    /// </summary>
    public class KeyErrorDetailsDto
    {
        /// <summary>
        /// Key ID
        /// </summary>
        public int KeyId { get; set; }
        
        /// <summary>
        /// Key name for display
        /// </summary>
        public string KeyName { get; set; } = string.Empty;
        
        /// <summary>
        /// Whether the key is currently disabled
        /// </summary>
        public bool IsDisabled { get; set; }
        
        /// <summary>
        /// When the key was disabled
        /// </summary>
        public DateTime? DisabledAt { get; set; }
        
        /// <summary>
        /// Fatal error information if any
        /// </summary>
        public FatalErrorDto? FatalError { get; set; }
        
        /// <summary>
        /// Recent warning errors
        /// </summary>
        public List<WarningErrorDto> RecentWarnings { get; set; } = new();
    }

    /// <summary>
    /// DTO for fatal error information
    /// </summary>
    public class FatalErrorDto
    {
        /// <summary>
        /// Type of error
        /// </summary>
        public string ErrorType { get; set; } = string.Empty;
        
        /// <summary>
        /// Number of occurrences
        /// </summary>
        public int Count { get; set; }
        
        /// <summary>
        /// When first seen
        /// </summary>
        public DateTime FirstSeen { get; set; }
        
        /// <summary>
        /// When last seen
        /// </summary>
        public DateTime LastSeen { get; set; }
        
        /// <summary>
        /// Last error message
        /// </summary>
        public string LastErrorMessage { get; set; } = string.Empty;
        
        /// <summary>
        /// Last HTTP status code
        /// </summary>
        public int? LastStatusCode { get; set; }
    }

    /// <summary>
    /// DTO for warning error information
    /// </summary>
    public class WarningErrorDto
    {
        /// <summary>
        /// Type of warning
        /// </summary>
        public string Type { get; set; } = string.Empty;
        
        /// <summary>
        /// Warning message
        /// </summary>
        public string Message { get; set; } = string.Empty;
        
        /// <summary>
        /// When the warning occurred
        /// </summary>
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Request DTO for clearing errors and re-enabling a key
    /// </summary>
    public class ClearErrorsRequest
    {
        /// <summary>
        /// Whether to re-enable the key
        /// </summary>
        public bool ReenableKey { get; set; } = true;
        
        /// <summary>
        /// Confirmation that the admin wants to re-enable
        /// </summary>
        public bool ConfirmReenable { get; set; }
        
        /// <summary>
        /// Reason for re-enabling
        /// </summary>
        public string? Reason { get; set; }
    }

    /// <summary>
    /// DTO for error statistics
    /// </summary>
    public class ErrorStatisticsDto
    {
        /// <summary>
        /// Total number of errors in the time window
        /// </summary>
        public int TotalErrors { get; set; }
        
        /// <summary>
        /// Number of fatal errors
        /// </summary>
        public int FatalErrors { get; set; }
        
        /// <summary>
        /// Number of warnings
        /// </summary>
        public int Warnings { get; set; }
        
        /// <summary>
        /// Number of currently disabled keys
        /// </summary>
        public int DisabledKeys { get; set; }
        
        /// <summary>
        /// Errors by type
        /// </summary>
        public Dictionary<string, int> ErrorsByType { get; set; } = new();
        
        /// <summary>
        /// Errors by provider
        /// </summary>
        public Dictionary<string, int> ErrorsByProvider { get; set; } = new();
        
        /// <summary>
        /// Time window for the statistics
        /// </summary>
        public TimeSpan TimeWindow { get; set; }
        
        /// <summary>
        /// When the statistics were generated
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }
}