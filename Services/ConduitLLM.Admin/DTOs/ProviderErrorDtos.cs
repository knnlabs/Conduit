using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

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
        [JsonRequired] public int KeyCredentialId { get; set; }
        
        /// <summary>
        /// Name of the key for display
        /// </summary>
        [JsonRequired] public string? KeyName { get; set; }
        
        /// <summary>
        /// ID of the provider
        /// </summary>
        [JsonRequired] public int ProviderId { get; set; }
        
        /// <summary>
        /// Name of the provider
        /// </summary>
        [JsonRequired] public string? ProviderName { get; set; }
        
        /// <summary>
        /// Type of error
        /// </summary>
        [JsonRequired] public string ErrorType { get; set; } = string.Empty;
        
        /// <summary>
        /// Error message
        /// </summary>
        [JsonRequired] public string ErrorMessage { get; set; } = string.Empty;
        
        /// <summary>
        /// HTTP status code if applicable
        /// </summary>
        [JsonRequired] public int? HttpStatusCode { get; set; }
        
        /// <summary>
        /// When the error occurred
        /// </summary>
        [JsonRequired] public DateTime OccurredAt { get; set; }
        
        /// <summary>
        /// Whether this is a fatal error
        /// </summary>
        [JsonRequired] public bool IsFatal { get; set; }
        
        /// <summary>
        /// Model that was being used
        /// </summary>
        [JsonRequired] public string? ModelName { get; set; }
    }

    /// <summary>
    /// DTO for provider-level error summary
    /// </summary>
    public class ProviderErrorSummaryDto
    {
        /// <summary>
        /// Provider ID
        /// </summary>
        [JsonRequired] public int ProviderId { get; set; }
        
        /// <summary>
        /// Provider name
        /// </summary>
        [JsonRequired] public string ProviderName { get; set; } = string.Empty;
        
        /// <summary>
        /// Total number of errors
        /// </summary>
        [JsonRequired] public int TotalErrors { get; set; }
        
        /// <summary>
        /// Number of fatal errors
        /// </summary>
        [JsonRequired] public int FatalErrors { get; set; }
        
        /// <summary>
        /// Number of warnings
        /// </summary>
        [JsonRequired] public int Warnings { get; set; }
        
        /// <summary>
        /// List of disabled key IDs
        /// </summary>
        [JsonRequired] public List<int> DisabledKeyIds { get; set; } = new();
        
        /// <summary>
        /// When the last error occurred
        /// </summary>
        [JsonRequired] public DateTime? LastError { get; set; }
        
        /// <summary>
        /// Number of currently disabled keys
        /// </summary>
        [JsonRequired] public int DisabledKeyCount { get; set; }
    }

    /// <summary>
    /// DTO for detailed key error information
    /// </summary>
    public class KeyErrorDetailsDto
    {
        /// <summary>
        /// Key ID
        /// </summary>
        [JsonRequired] public int KeyId { get; set; }
        
        /// <summary>
        /// Key name for display
        /// </summary>
        [JsonRequired] public string? KeyName { get; set; }
        
        /// <summary>
        /// Whether the key is currently disabled
        /// </summary>
        [JsonRequired] public bool IsDisabled { get; set; }
        
        /// <summary>
        /// When the key was disabled
        /// </summary>
        [JsonRequired] public DateTime? DisabledAt { get; set; }
        
        /// <summary>
        /// Fatal error information if any
        /// </summary>
        [JsonRequired] public FatalErrorDto? FatalError { get; set; }
        
        /// <summary>
        /// Recent warning errors
        /// </summary>
        [JsonRequired] public List<WarningErrorDto> RecentWarnings { get; set; } = new();
    }

    /// <summary>
    /// DTO for fatal error information
    /// </summary>
    public class FatalErrorDto
    {
        /// <summary>
        /// Type of error
        /// </summary>
        [JsonRequired] public string ErrorType { get; set; } = string.Empty;
        
        /// <summary>
        /// Number of occurrences
        /// </summary>
        [JsonRequired] public int Count { get; set; }
        
        /// <summary>
        /// When first seen
        /// </summary>
        [JsonRequired] public DateTime FirstSeen { get; set; }
        
        /// <summary>
        /// When last seen
        /// </summary>
        [JsonRequired] public DateTime LastSeen { get; set; }
        
        /// <summary>
        /// Last error message
        /// </summary>
        [JsonRequired] public string LastErrorMessage { get; set; } = string.Empty;
        
        /// <summary>
        /// Last HTTP status code
        /// </summary>
        [JsonRequired] public int? LastStatusCode { get; set; }
    }

    /// <summary>
    /// DTO for warning error information
    /// </summary>
    public class WarningErrorDto
    {
        /// <summary>
        /// Type of warning
        /// </summary>
        [JsonRequired] public string Type { get; set; } = string.Empty;
        
        /// <summary>
        /// Warning message
        /// </summary>
        [JsonRequired] public string Message { get; set; } = string.Empty;
        
        /// <summary>
        /// When the warning occurred
        /// </summary>
        [JsonRequired] public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Request DTO for clearing errors and re-enabling a key
    /// </summary>
    public class ClearErrorsRequest
    {
        /// <summary>
        /// Whether to re-enable the key
        /// </summary>
        [JsonRequired] public bool ReenableKey { get; set; } = true;
        
        /// <summary>
        /// Confirmation that the admin wants to re-enable
        /// </summary>
        [JsonRequired] public bool ConfirmReenable { get; set; }
        
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
        [JsonRequired] public int TotalErrors { get; set; }
        
        /// <summary>
        /// Number of fatal errors
        /// </summary>
        [JsonRequired] public int FatalErrors { get; set; }
        
        /// <summary>
        /// Number of warnings
        /// </summary>
        [JsonRequired] public int Warnings { get; set; }
        
        /// <summary>
        /// Number of currently disabled keys
        /// </summary>
        [JsonRequired] public int DisabledKeys { get; set; }
        
        /// <summary>
        /// Errors by type
        /// </summary>
        [JsonRequired] public Dictionary<string, int> ErrorsByType { get; set; } = new();
        
        /// <summary>
        /// Errors by provider
        /// </summary>
        [JsonRequired] public Dictionary<string, int> ErrorsByProvider { get; set; } = new();
        
        /// <summary>
        /// Time window for the statistics
        /// </summary>
        [JsonRequired] public TimeSpan TimeWindow { get; set; }
        
        /// <summary>
        /// When the statistics were generated
        /// </summary>
        [JsonRequired] public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }
}
