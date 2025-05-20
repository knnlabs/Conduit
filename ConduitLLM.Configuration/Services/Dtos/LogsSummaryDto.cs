using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.Services.Dtos
{
    /// <summary>
    /// Data transfer object for logs summary statistics
    /// </summary>
    /// <remarks>
    /// IMPORTANT: There are two LogsSummaryDto classes in the project:
    /// 1. ConduitLLM.Configuration.Services.Dtos.LogsSummaryDto (this one)
    /// 2. ConduitLLM.Configuration.DTOs.LogsSummaryDto
    ///
    /// When referencing either class, use the fully qualified name to avoid ambiguity.
    /// This class is used internally by the RequestLogService, while the DTOs version
    /// is primarily for API/client consumption.
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
        /// Request count by model name (legacy format)
        /// </summary>
        public Dictionary<string, int> RequestsByModelDict { get; set; } = new Dictionary<string, int>();
        
        /// <summary>
        /// Cost by model name
        /// </summary>
        public Dictionary<string, decimal> CostByModel { get; set; } = new Dictionary<string, decimal>();
        
        /// <summary>
        /// Request count by virtual key ID
        /// </summary>
        public Dictionary<int, KeySummary> RequestsByKey { get; set; } = new Dictionary<int, KeySummary>();
        
        /// <summary>
        /// Success rate percentage
        /// </summary>
        public double SuccessRate { get; set; }
        
        /// <summary>
        /// Requests by status code
        /// </summary>
        public Dictionary<int, int> RequestsByStatus { get; set; } = new Dictionary<int, int>();
        
        /// <summary>
        /// Number of successful requests
        /// </summary>
        public int SuccessfulRequests { get; set; }
        
        /// <summary>
        /// Number of failed requests
        /// </summary>
        public int FailedRequests { get; set; }
        
        /// <summary>
        /// Daily statistics
        /// </summary>
        public List<DailyStatsDto> DailyStats { get; set; } = new List<DailyStatsDto>();
        
        /// <summary>
        /// Model usage statistics
        /// </summary>
        public List<RequestsByModelDto> RequestsByModel { get; set; } = new List<RequestsByModelDto>();
    }
    
    /// <summary>
    /// Summary statistics for a virtual key
    /// </summary>
    public class KeySummary
    {
        /// <summary>
        /// Name of the virtual key
        /// </summary>
        public string KeyName { get; set; } = string.Empty;
        
        /// <summary>
        /// Request count for this key
        /// </summary>
        public int RequestCount { get; set; }
        
        /// <summary>
        /// Total cost for this key
        /// </summary>
        public decimal TotalCost { get; set; }
    }
}