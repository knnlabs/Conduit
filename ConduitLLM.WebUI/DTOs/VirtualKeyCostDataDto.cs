using System;

using ConfigDto = ConduitLLM.Configuration.DTOs.Costs;

namespace ConduitLLM.WebUI.DTOs
{
    /// <summary>
    /// Type alias for the VirtualKeyCostDataDto from ConduitLLM.Configuration.DTOs.Costs
    /// This exists to maintain backward compatibility while consolidating duplicate definitions.
    /// </summary>
    public class VirtualKeyCostDataDto : ConfigDto.VirtualKeyCostDataDto
    {
        /// <summary>
        /// Virtual key name alias (for compatibility)
        /// </summary>
        public string VirtualKeyName
        {
            get => KeyName;
            set => KeyName = value;
        }

        /// <summary>
        /// Total cost alias (for compatibility)
        /// </summary>
        public decimal TotalCost
        {
            get => Cost;
            set => Cost = value;
        }

        /// <summary>
        /// Total requests alias (for compatibility)
        /// </summary>
        public int TotalRequests
        {
            get => RequestCount;
            set => RequestCount = value;
        }

        /// <summary>
        /// Number of input tokens (WebUI-specific property)
        /// </summary>
        public int InputTokens { get; set; }

        /// <summary>
        /// Total input tokens alias (for compatibility)
        /// </summary>
        public int TotalInputTokens
        {
            get => InputTokens;
            set => InputTokens = value;
        }

        /// <summary>
        /// Number of output tokens (WebUI-specific property)
        /// </summary>
        public int OutputTokens { get; set; }

        /// <summary>
        /// Total output tokens alias (for compatibility)
        /// </summary>
        public int TotalOutputTokens
        {
            get => OutputTokens;
            set => OutputTokens = value;
        }

        /// <summary>
        /// Average response time in milliseconds (WebUI-specific property)
        /// </summary>
        public double AverageResponseTimeMs { get; set; }

        /// <summary>
        /// Last time the key was used (WebUI-specific property)
        /// </summary>
        public DateTime? LastUsedAt { get; set; }

        /// <summary>
        /// Last request time (alias for LastUsedAt)
        /// </summary>
        public DateTime LastRequestTime
        {
            get => LastUsedAt ?? DateTime.MinValue;
            set => LastUsedAt = value;
        }

        /// <summary>
        /// Last request date (alias for LastUsedAt)
        /// </summary>
        public DateTime LastRequestDate
        {
            get => LastUsedAt ?? DateTime.MinValue;
            set => LastUsedAt = value;
        }

        /// <summary>
        /// Time when the key was created (WebUI-specific property)
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// First request time (alias for CreatedAt)
        /// </summary>
        public DateTime FirstRequestTime
        {
            get => CreatedAt;
            set => CreatedAt = value;
        }

        /// <summary>
        /// Number of requests in the last day (WebUI-specific property)
        /// </summary>
        public int LastDayRequests { get; set; }
    }
}
