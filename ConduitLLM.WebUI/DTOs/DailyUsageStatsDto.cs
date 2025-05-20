using System;

namespace ConduitLLM.WebUI.DTOs
{
    /// <summary>
    /// Daily usage statistics used by the WebUI dashboard
    /// </summary>
    public class DailyUsageStatsDto
    {
        /// <summary>
        /// Date of the statistics record
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Model name for this record
        /// </summary>
        public string ModelName { get; set; } = string.Empty;

        /// <summary>
        /// Number of requests for this day
        /// </summary>
        public int RequestCount { get; set; }

        /// <summary>
        /// Number of input tokens
        /// </summary>
        public int InputTokens { get; set; }

        /// <summary>
        /// Number of output tokens
        /// </summary>
        public int OutputTokens { get; set; }

        /// <summary>
        /// Total cost for this day
        /// </summary>
        public decimal Cost { get; set; }

        /// <summary>
        /// Total cost for backwards compatibility
        /// </summary>
        public decimal TotalCost
        {
            get => Cost;
            set => Cost = value;
        }
    }
}
