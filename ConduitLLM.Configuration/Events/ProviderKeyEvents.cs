using System;

namespace ConduitLLM.Configuration.Events
{
    /// <summary>
    /// Event raised when a provider key is disabled due to errors
    /// </summary>
    public class ProviderKeyDisabledEvent
    {
        /// <summary>
        /// ID of the disabled key
        /// </summary>
        public int KeyId { get; set; }
        
        /// <summary>
        /// ID of the provider
        /// </summary>
        public int ProviderId { get; set; }
        
        /// <summary>
        /// Reason for disabling the key
        /// </summary>
        public string Reason { get; set; } = string.Empty;
        
        /// <summary>
        /// Type of error that caused the disable
        /// </summary>
        public string ErrorType { get; set; } = string.Empty;
        
        /// <summary>
        /// When the key was disabled
        /// </summary>
        public DateTime DisabledAt { get; set; }
        
        /// <summary>
        /// Whether this was an automatic disable
        /// </summary>
        public bool IsAutomatic { get; set; } = true;
    }
    
    /// <summary>
    /// Event raised when a provider key is re-enabled
    /// </summary>
    public class ProviderKeyReenabledEvent
    {
        /// <summary>
        /// ID of the re-enabled key
        /// </summary>
        public int KeyId { get; set; }
        
        /// <summary>
        /// ID of the provider
        /// </summary>
        public int ProviderId { get; set; }
        
        /// <summary>
        /// Who re-enabled the key
        /// </summary>
        public string ReenabledBy { get; set; } = string.Empty;
        
        /// <summary>
        /// Reason for re-enabling
        /// </summary>
        public string Reason { get; set; } = string.Empty;
        
        /// <summary>
        /// When the key was re-enabled
        /// </summary>
        public DateTime ReenabledAt { get; set; } = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Event raised when provider errors exceed alert thresholds
    /// </summary>
    public class ProviderErrorAlertEvent
    {
        /// <summary>
        /// ID of the provider
        /// </summary>
        public int ProviderId { get; set; }
        
        /// <summary>
        /// Name of the provider
        /// </summary>
        public string ProviderName { get; set; } = string.Empty;
        
        /// <summary>
        /// Type of error that triggered the alert
        /// </summary>
        public string ErrorType { get; set; } = string.Empty;
        
        /// <summary>
        /// Number of errors
        /// </summary>
        public int ErrorCount { get; set; }
        
        /// <summary>
        /// Time window for the error count
        /// </summary>
        public TimeSpan TimeWindow { get; set; }
        
        /// <summary>
        /// Alert message
        /// </summary>
        public string AlertMessage { get; set; } = string.Empty;
        
        /// <summary>
        /// When the alert was triggered
        /// </summary>
        public DateTime AlertedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Severity level (Warning, Error, Critical)
        /// </summary>
        public string Severity { get; set; } = "Warning";
    }
}