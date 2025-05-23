using System;

namespace ConduitLLM.WebUI.DTOs
{
    /// <summary>
    /// Aggregated usage summary for a virtual key
    /// </summary>
    public class KeyAggregateSummary
    {
        /// <summary>
        /// ID of the virtual key
        /// </summary>
        public int VirtualKeyId { get; set; }

        /// <summary>
        /// Name of the virtual key
        /// </summary>
        public string KeyName { get; set; } = string.Empty;

        /// <summary>
        /// Name of the virtual key (alias for VirtualKeyName for backward compatibility)
        /// </summary>
        public string VirtualKeyName { get => KeyName; set => KeyName = value; }

        /// <summary>
        /// Total number of requests made with this key
        /// </summary>
        public int TotalRequests { get; set; }

        /// <summary>
        /// Total cost of requests made with this key
        /// </summary>
        public decimal TotalCost { get; set; }

        /// <summary>
        /// Total input tokens processed with this key
        /// </summary>
        public int TotalInputTokens { get; set; }

        /// <summary>
        /// Total output tokens generated with this key
        /// </summary>
        public int TotalOutputTokens { get; set; }

        /// <summary>
        /// Date of the most recent request
        /// </summary>
        public DateTime? LastUsed { get; set; }

        /// <summary>
        /// Whether the key is currently active
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Current spend amount for budgeting
        /// </summary>
        public decimal CurrentSpend { get; set; }

        /// <summary>
        /// Maximum budget allowed
        /// </summary>
        public decimal? MaxBudget { get; set; }

        /// <summary>
        /// Average response time for this key
        /// </summary>
        public double AverageResponseTime { get; set; }

        /// <summary>
        /// Number of recent requests (for UI display)
        /// </summary>
        public int RecentRequests { get; set; }
    }
}