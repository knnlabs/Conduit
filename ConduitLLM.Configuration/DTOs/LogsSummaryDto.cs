using System;
using System.Collections.Generic;
using System.Linq;


namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Data transfer object for logs summary statistics
    /// </summary>
    public class LogsSummaryDto
    {
        /// <summary>
        /// Total number of requests in the period
        /// </summary>
        public int TotalRequests { get; set; }

        /// <summary>
        /// Total cost of all requests in the period
        /// </summary>
        public decimal EstimatedCost { get; set; }

        /// <summary>
        /// Total input tokens across all requests
        /// </summary>
        public int InputTokens { get; set; }

        /// <summary>
        /// Total output tokens across all requests
        /// </summary>
        public int OutputTokens { get; set; }

        /// <summary>
        /// Total tokens (input + output) for all requests
        /// </summary>
        public int TotalTokens => InputTokens + OutputTokens;

        /// <summary>
        /// Average response time in milliseconds
        /// </summary>
        public double AverageResponseTime { get; set; }

        /// <summary>
        /// Number of successful requests
        /// </summary>
        public int SuccessfulRequests { get; set; }

        /// <summary>
        /// Number of failed requests
        /// </summary>
        public int FailedRequests { get; set; }

        /// <summary>
        /// Success rate percentage
        /// </summary>
        public double SuccessRate => TotalRequests > 0 ? (double)SuccessfulRequests / TotalRequests * 100 : 0;

        /// <summary>
        /// Date of the most recent request
        /// </summary>
        public DateTime? LastRequestDate { get; set; }

        /// <summary>
        /// Request count by model name
        /// </summary>
        public Dictionary<string, int> RequestsByModel { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// Cost by model name
        /// </summary>
        public Dictionary<string, decimal> CostByModel { get; set; } = new Dictionary<string, decimal>();

        /// <summary>
        /// Request count by status code
        /// </summary>
        public Dictionary<int, int> RequestsByStatus { get; set; } = new Dictionary<int, int>();

        /// <summary>
        /// Daily statistics
        /// </summary>
        public List<DailyUsageStatsDto> DailyStats { get; set; } = new List<DailyUsageStatsDto>();


    }

    /// <summary>
    /// Daily usage statistics for the logs summary
    /// </summary>
    public class DailyUsageStatsDto
    {
        /// <summary>
        /// Date for the statistics
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Model ID for this record
        /// </summary>
        public string ModelId { get; set; } = string.Empty;


        /// <summary>
        /// Number of requests on this date
        /// </summary>
        public int RequestCount { get; set; }

        /// <summary>
        /// Number of input tokens on this date
        /// </summary>
        public int InputTokens { get; set; }

        /// <summary>
        /// Number of output tokens on this date
        /// </summary>
        public int OutputTokens { get; set; }

        /// <summary>
        /// Total cost for this date
        /// </summary>
        public decimal Cost { get; set; }

    }
}
