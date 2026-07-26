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
        /// Raw provider error text, truncated by consumers before display.
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;
        
        /// <summary>
        /// When the key was disabled
        /// </summary>
        public DateTime DisabledAt { get; set; }
        
        /// <summary>
        /// Whether this was an automatic disable
        /// </summary>
        public bool IsAutomatic { get; set; } = true;

        /// <summary>
        /// All key IDs affected by this operation. Contains KeyId for a single-key disable.
        /// </summary>
        public IReadOnlyList<int> AffectedKeyIds { get; set; } = Array.Empty<int>();

        /// <summary>
        /// Shared provider account group, or 0 for an ungrouped key.
        /// </summary>
        public short ProviderAccountGroup { get; set; }
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

        /// <summary>
        /// All key IDs restored by this operation.
        /// </summary>
        public IReadOnlyList<int> AffectedKeyIds { get; set; } = Array.Empty<int>();

        /// <summary>
        /// Shared provider account group, or 0 for an ungrouped key.
        /// </summary>
        public short ProviderAccountGroup { get; set; }
    }
}
