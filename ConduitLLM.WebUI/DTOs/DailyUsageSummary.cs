using System;

namespace ConduitLLM.WebUI.DTOs
{
    /// <summary>
    /// Summary of usage statistics for a specific day
    /// </summary>
    public class DailyUsageSummary
    {
        /// <summary>
        /// Date for the statistics
        /// </summary>
        public DateTime Date { get; set; }
        
        /// <summary>
        /// Total number of requests on this date
        /// </summary>
        public int RequestCount { get; set; }
        
        /// <summary>
        /// Total cost of requests on this date
        /// </summary>
        public decimal TotalCost { get; set; }
        
        /// <summary>
        /// Total input tokens processed on this date
        /// </summary>
        public int InputTokens { get; set; }
        
        /// <summary>
        /// Total output tokens generated on this date
        /// </summary>
        public int OutputTokens { get; set; }

        /// <summary>
        /// Total tokens (input + output)
        /// </summary>
        public int TotalTokens
        {
            get => InputTokens + OutputTokens;
            set { /* Setter for backward compatibility */ }
        }
        
        /// <summary>
        /// Average response time in milliseconds for requests on this date
        /// </summary>
        public double AverageResponseTimeMs { get; set; }
        
        /// <summary>
        /// Number of successful requests on this date
        /// </summary>
        public int SuccessfulRequests { get; set; }
        
        /// <summary>
        /// Number of failed requests on this date
        /// </summary>
        public int FailedRequests { get; set; }
        
        /// <summary>
        /// Virtual key ID if this summary is for a specific key, null otherwise
        /// </summary>
        public int? VirtualKeyId { get; set; }
        
        /// <summary>
        /// Name of the model for this summary, if filtered by model
        /// </summary>
        public string? ModelName { get; set; }
    }
}