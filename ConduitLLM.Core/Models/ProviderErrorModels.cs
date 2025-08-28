using System;
using System.Collections.Generic;

namespace ConduitLLM.Core.Models
{
    /// <summary>
    /// Categorizes provider errors by severity and type
    /// </summary>
    public enum ProviderErrorType
    {
        // Fatal Errors - Auto-disable key (1-9)
        /// <summary>
        /// 401 Unauthorized - Invalid or expired API key
        /// </summary>
        InvalidApiKey = 1,
        
        /// <summary>
        /// 402 Payment Required or insufficient balance/quota messages
        /// </summary>
        InsufficientBalance = 2,
        
        /// <summary>
        /// 403 Forbidden - Permission denied (not balance related)
        /// </summary>
        AccessForbidden = 3,

        // Warnings - Track but don't disable (10-19)
        /// <summary>
        /// 429 Too Many Requests - Rate limit exceeded
        /// </summary>
        RateLimitExceeded = 10,
        
        /// <summary>
        /// 404 Not Found - Model or resource not found
        /// </summary>
        ModelNotFound = 11,
        
        /// <summary>
        /// 503 Service Unavailable - Provider service is down
        /// </summary>
        ServiceUnavailable = 12,

        // Transient Errors - May not track (20-29)
        /// <summary>
        /// Network connectivity issues
        /// </summary>
        NetworkError = 20,
        
        /// <summary>
        /// Request timeout
        /// </summary>
        Timeout = 21,
        
        /// <summary>
        /// Unclassified error
        /// </summary>
        Unknown = 99
    }

    /// <summary>
    /// Detailed information about a provider error occurrence
    /// </summary>
    public class ProviderErrorInfo
    {
        /// <summary>
        /// ID of the key credential that caused the error
        /// </summary>
        public int KeyCredentialId { get; set; }
        
        /// <summary>
        /// ID of the provider
        /// </summary>
        public int ProviderId { get; set; }
        
        /// <summary>
        /// Type of error that occurred
        /// </summary>
        public ProviderErrorType ErrorType { get; set; }
        
        /// <summary>
        /// Detailed error message from the provider
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;
        
        /// <summary>
        /// HTTP status code if applicable
        /// </summary>
        public int? HttpStatusCode { get; set; }
        
        /// <summary>
        /// Name of the provider for logging
        /// </summary>
        public string? ProviderName { get; set; }
        
        /// <summary>
        /// Model that was being used when error occurred
        /// </summary>
        public string? ModelName { get; set; }
        
        /// <summary>
        /// When the error occurred
        /// </summary>
        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Whether this is a fatal error that should disable the key
        /// </summary>
        public bool IsFatal => (int)ErrorType <= 9;
        
        /// <summary>
        /// Request ID for correlation
        /// </summary>
        public string? RequestId { get; set; }
        
        /// <summary>
        /// Retry attempt number (0 for first attempt)
        /// </summary>
        public int RetryAttempt { get; set; }
    }

    /// <summary>
    /// Policy for when to disable a key based on error type
    /// </summary>
    public class DisablePolicy
    {
        /// <summary>
        /// Whether to disable the key immediately on first occurrence
        /// </summary>
        public bool DisableImmediately { get; set; }
        
        /// <summary>
        /// Number of occurrences required before disabling
        /// </summary>
        public int RequiredOccurrences { get; set; } = 1;
        
        /// <summary>
        /// Time window for counting occurrences
        /// </summary>
        public TimeSpan TimeWindow { get; set; } = TimeSpan.FromMinutes(5);
        
        /// <summary>
        /// Whether the key requires manual re-enabling
        /// </summary>
        public bool RequiresManualReenable { get; set; } = true;
    }

    /// <summary>
    /// Policy for when to alert on warning errors
    /// </summary>
    public class AlertPolicy
    {
        /// <summary>
        /// Number of occurrences before alerting
        /// </summary>
        public int AlertThreshold { get; set; }
        
        /// <summary>
        /// Time window for counting occurrences
        /// </summary>
        public TimeSpan TimeWindow { get; set; }
        
        /// <summary>
        /// Message to include in alert
        /// </summary>
        public string AlertMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// Configuration for error thresholds and policies
    /// </summary>
    public static class ErrorThresholdConfiguration
    {
        /// <summary>
        /// Policies for fatal errors that may disable keys
        /// </summary>
        public static readonly Dictionary<ProviderErrorType, DisablePolicy> FatalErrorPolicies = new()
        {
            [ProviderErrorType.InvalidApiKey] = new DisablePolicy
            {
                DisableImmediately = true,
                RequiresManualReenable = true
            },
            
            [ProviderErrorType.InsufficientBalance] = new DisablePolicy
            {
                DisableImmediately = false,
                RequiredOccurrences = 2,
                TimeWindow = TimeSpan.FromMinutes(5),
                RequiresManualReenable = true
            },
            
            [ProviderErrorType.AccessForbidden] = new DisablePolicy
            {
                DisableImmediately = false,
                RequiredOccurrences = 3,
                TimeWindow = TimeSpan.FromMinutes(10),
                RequiresManualReenable = true
            }
        };
        
        /// <summary>
        /// Alert policies for warning errors
        /// </summary>
        public static readonly Dictionary<ProviderErrorType, AlertPolicy> WarningAlertPolicies = new()
        {
            [ProviderErrorType.RateLimitExceeded] = new AlertPolicy
            {
                AlertThreshold = 10,
                TimeWindow = TimeSpan.FromMinutes(5),
                AlertMessage = "High rate limit pressure detected"
            },
            
            [ProviderErrorType.ServiceUnavailable] = new AlertPolicy
            {
                AlertThreshold = 5,
                TimeWindow = TimeSpan.FromMinutes(10),
                AlertMessage = "Provider service experiencing issues"
            }
        };
    }
}