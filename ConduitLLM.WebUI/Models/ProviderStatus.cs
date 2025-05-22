using System;

namespace ConduitLLM.WebUI.Models
{
    /// <summary>
    /// Represents the status of a provider
    /// </summary>
    public class ProviderStatus
    {
        /// <summary>
        /// Status type for a provider
        /// </summary>
        public enum StatusType
        {
            /// <summary>
            /// Provider is online
            /// </summary>
            Online = 0,

            /// <summary>
            /// Provider is offline
            /// </summary>
            Offline = 1,

            /// <summary>
            /// Provider status cannot be determined
            /// </summary>
            Unknown = 2
        }
        
        /// <summary>
        /// The status of the provider
        /// </summary>
        public StatusType Status { get; set; } = StatusType.Unknown;
        
        /// <summary>
        /// Whether the provider is online (maintained for compatibility)
        /// </summary>
        public bool IsOnline 
        { 
            get => Status == StatusType.Online;
            set => Status = value ? StatusType.Online : StatusType.Offline;
        }
        
        /// <summary>
        /// The status message for the provider
        /// </summary>
        public string StatusMessage { get; set; } = string.Empty;
        
        /// <summary>
        /// When the status was last checked
        /// </summary>
        public DateTime LastCheckedUtc { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// The response time of the health check in milliseconds
        /// </summary>
        public double ResponseTimeMs { get; set; }
        
        /// <summary>
        /// The category of error if the provider is offline
        /// </summary>
        public string? ErrorCategory { get; set; }
        
        /// <summary>
        /// The endpoint URL that was checked
        /// </summary>
        public string? EndpointUrl { get; set; }
    }
}