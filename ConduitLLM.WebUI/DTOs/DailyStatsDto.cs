using System;

namespace ConduitLLM.WebUI.DTOs
{
    /// <summary>
    /// Daily statistics data for the logs summary
    /// </summary>
    public class DailyStatsDto
    {
        /// <summary>
        /// Date for the statistics
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Number of requests on this date
        /// </summary>
        public int RequestCount { get; set; }

        /// <summary>
        /// Cost for this date
        /// </summary>
        public decimal Cost { get; set; }

        /// <summary>
        /// Input tokens for this date
        /// </summary>
        public int InputTokens { get; set; }

        /// <summary>
        /// Output tokens for this date
        /// </summary>
        public int OutputTokens { get; set; }

        /// <summary>
        /// Average response time in milliseconds
        /// </summary>
        public double AverageResponseTimeMs { get; set; }
    }
}