using System;

namespace ConduitLLM.Configuration.Services.Dtos
{
    /// <summary>
    /// Data transfer object for daily log statistics
    /// </summary>
    public class DailyStatsDto
    {
        /// <summary>
        /// The date for these statistics
        /// </summary>
        public DateTime Date { get; set; }
        
        /// <summary>
        /// Total number of requests on this date
        /// </summary>
        public int RequestCount { get; set; }
        
        /// <summary>
        /// Total cost of all requests on this date
        /// </summary>
        public decimal Cost { get; set; }
        
        /// <summary>
        /// Average response time in milliseconds for requests on this date
        /// </summary>
        public double AverageResponseTime { get; set; }
        
        /// <summary>
        /// Total input tokens for requests on this date
        /// </summary>
        public int InputTokens { get; set; }
        
        /// <summary>
        /// Total output tokens for requests on this date
        /// </summary>
        public int OutputTokens { get; set; }
        
        /// <summary>
        /// Total tokens (input + output) for requests on this date
        /// </summary>
        public int TotalTokens => InputTokens + OutputTokens;
        
        /// <summary>
        /// Number of successful requests on this date
        /// </summary>
        public int SuccessfulRequests { get; set; }
        
        /// <summary>
        /// Number of failed requests on this date
        /// </summary>
        public int FailedRequests { get; set; }
    }
}