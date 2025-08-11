using System;

namespace ConduitLLM.Configuration.DTOs.VirtualKey
{
    /// <summary>
    /// Data Transfer Object representing usage information for a Virtual Key
    /// </summary>
    public class VirtualKeyUsageDto
    {
        /// <summary>
        /// The virtual key identifier (database ID)
        /// </summary>
        public int KeyId { get; set; }

        /// <summary>
        /// Human-readable name of the virtual key
        /// </summary>
        public string KeyName { get; set; } = string.Empty;

        /// <summary>
        /// The virtual key group ID this key belongs to
        /// </summary>
        public int GroupId { get; set; }

        /// <summary>
        /// Human-readable name of the virtual key group
        /// </summary>
        public string GroupName { get; set; } = string.Empty;

        /// <summary>
        /// Current balance available for this key (from group balance)
        /// </summary>
        public decimal Balance { get; set; }

        /// <summary>
        /// Total lifetime credits added to the group
        /// </summary>
        public decimal LifetimeCreditsAdded { get; set; }

        /// <summary>
        /// Total lifetime amount spent from this group
        /// </summary>
        public decimal LifetimeSpent { get; set; }

        /// <summary>
        /// Total number of requests made with this specific key
        /// </summary>
        public int TotalRequests { get; set; }

        /// <summary>
        /// Total tokens consumed by this specific key
        /// </summary>
        public long TotalTokens { get; set; }

        /// <summary>
        /// Indicates if the key is currently enabled
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Expiration date of the key, if set
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Date and time when the key was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Date and time when the key was last used
        /// </summary>
        public DateTime? LastUsedAt { get; set; }

        /// <summary>
        /// Rate limit in requests per minute, if set
        /// </summary>
        public int? RateLimitRpm { get; set; }

        /// <summary>
        /// Rate limit in requests per day, if set
        /// </summary>
        public int? RateLimitRpd { get; set; }

        /// <summary>
        /// Comma-separated list of allowed model IDs, if restricted
        /// </summary>
        public string? AllowedModels { get; set; }
    }
}