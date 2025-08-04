using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConduitLLM.Configuration.Entities
{
    /// <summary>
    /// Represents a point-in-time health status record for an LLM provider.
    /// </summary>
    public class ProviderHealthRecord
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
        /// The unique identifier for the health record.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The ID of the provider that was checked.
        /// </summary>
        public int ProviderId { get; set; }

        /// <summary>
        /// Navigation property to the provider.
        /// </summary>
        [ForeignKey("ProviderId")]
        public virtual Provider Provider { get; set; } = null!;

        /// <summary>
        /// The status of the provider.
        /// </summary>
        public StatusType Status { get; set; } = StatusType.Unknown;

        /// <summary>
        /// Indicates whether the provider was online at the time of checking.
        /// This property is maintained for backward compatibility.
        /// </summary>
        public bool IsOnline
        {
            get => Status == StatusType.Online;
            set => Status = value ? StatusType.Online : StatusType.Offline;
        }

        /// <summary>
        /// A human-readable status message describing the health check result.
        /// </summary>
        public string StatusMessage { get; set; } = string.Empty;

        /// <summary>
        /// The UTC timestamp when the health check was performed.
        /// </summary>
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The response time in milliseconds for the health check request.
        /// </summary>
        public double ResponseTimeMs { get; set; }

        /// <summary>
        /// Categorization of the error if the check failed (e.g., Network, Authentication, RateLimit).
        /// </summary>
        public string? ErrorCategory { get; set; }

        /// <summary>
        /// Detailed error information if the check failed.
        /// </summary>
        public string? ErrorDetails { get; set; }

        /// <summary>
        /// The endpoint URL that was checked.
        /// </summary>
        public string? EndpointUrl { get; set; }
    }
}
