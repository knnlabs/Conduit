using System;

namespace ConduitLLM.WebUI.DTOs
{
    /// <summary>
    /// Data transfer object for logs summary statistics used in the WebUI.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: There are multiple LogsSummaryDto classes in the project:
    /// 1. ConduitLLM.WebUI.DTOs.LogsSummaryDto (this one)
    /// 2. ConduitLLM.Configuration.DTOs.LogsSummaryDto
    /// 3. ConduitLLM.Configuration.Services.Dtos.LogsSummaryDto
    ///
    /// When referencing any of these classes, use the fully qualified name to avoid ambiguity.
    /// This class is primarily for WebUI consumption and adapts the API version.
    /// </remarks>
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
        /// Start date of the period
        /// </summary>
        public DateTime StartDate { get; set; }
        
        /// <summary>
        /// End date of the period
        /// </summary>
        public DateTime EndDate { get; set; }
        
        /// <summary>
        /// Date of the most recent request
        /// </summary>
        public DateTime? LastRequestDate { get; set; }
        
        /// <summary>
        /// Success rate percentage
        /// </summary>
        public double SuccessRate
        {
            get => TotalRequests > 0 ? (double)SuccessfulRequests / TotalRequests * 100 : 0;
            set { /* Setter for backward compatibility */ }
        }

        /// <summary>
        /// Number of successful requests
        /// </summary>
        public int SuccessfulRequests { get; set; }

        /// <summary>
        /// Number of failed requests
        /// </summary>
        public int FailedRequests { get; set; }
    }
}
