using System;
using System.Collections.Generic;

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
        public decimal TotalCost { get; set; }
        
        /// <summary>
        /// Total input tokens across all requests
        /// </summary>
        public int TotalInputTokens { get; set; }
        
        /// <summary>
        /// Total output tokens across all requests
        /// </summary>
        public int TotalOutputTokens { get; set; }
        
        /// <summary>
        /// Total tokens (input + output) for all requests
        /// </summary>
        public int TotalTokens => TotalInputTokens + TotalOutputTokens;
        
        /// <summary>
        /// Average response time in milliseconds
        /// </summary>
        public double AverageResponseTimeMs { get; set; }
        
        /// <summary>
        /// Start date of the period
        /// </summary>
        public DateTime StartDate { get; set; }
        
        /// <summary>
        /// End date of the period
        /// </summary>
        public DateTime EndDate { get; set; }
        
        /// <summary>
        /// Number of days in the period
        /// </summary>
        public int TotalDays { get; set; }
        
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
        
        /// <summary>
        /// Daily breakdown of requests
        /// </summary>
        public List<DailyStatsDto> DailyBreakdown { get; set; } = new List<DailyStatsDto>();
        
        /// <summary>
        /// Breakdown by model
        /// </summary>
        public List<RequestsByModelDto> ModelBreakdown { get; set; } = new List<RequestsByModelDto>();
        
        /// <summary>
        /// The most used model in the period
        /// </summary>
        public string? TopModel { get; set; }
        
        /// <summary>
        /// Requests in the last 24 hours
        /// </summary>
        public int RequestsLast24Hours { get; set; }
        
        /// <summary>
        /// Requests in the last 7 days
        /// </summary>
        public int RequestsLast7Days { get; set; }
        
        /// <summary>
        /// Requests in the last 30 days
        /// </summary>
        public int RequestsLast30Days { get; set; }
    }
}
