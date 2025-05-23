using System;
using System.Collections.Generic;
using System.Linq;
using ConduitLLM.Configuration.Services.Dtos;

#pragma warning disable CS0618 // Type or member is obsolete - We're managing the migration process
namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Data transfer object for logs summary statistics
    /// </summary>
    /// <remarks>
    /// IMPORTANT: There are two LogsSummaryDto classes in the project:
    /// 1. ConduitLLM.Configuration.DTOs.LogsSummaryDto (this one)
    /// 2. ConduitLLM.Configuration.Services.Dtos.LogsSummaryDto
    ///
    /// When referencing either class, use the fully qualified name to avoid ambiguity.
    /// This class is primarily for API/client consumption, while the Services.Dtos version
    /// is used internally by the RequestLogService.
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
        public double SuccessRate
        {
            get => TotalRequests > 0 ? (double)SuccessfulRequests / TotalRequests * 100 : 0;
            set { /* Setter for backward compatibility */ }
        }

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
        
        // Backwards compatibility properties for Services.Dtos.LogsSummaryDto
        
        /// <summary>
        /// Total cost (alias for EstimatedCost)
        /// </summary>
        public decimal TotalCost
        {
            get => EstimatedCost;
            set => EstimatedCost = value;
        }
        
        /// <summary>
        /// Total input tokens (alias for InputTokens)
        /// </summary>
        public int TotalInputTokens
        {
            get => InputTokens;
            set => InputTokens = value;
        }
        
        /// <summary>
        /// Total output tokens (alias for OutputTokens)
        /// </summary>
        public int TotalOutputTokens
        {
            get => OutputTokens;
            set => OutputTokens = value;
        }
        
        /// <summary>
        /// Average response time (alias for AverageResponseTime)
        /// </summary>
        public double AverageResponseTimeMs
        {
            get => AverageResponseTime;
            set => AverageResponseTime = value;
        }

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
        /// Model name (compatibility property)
        /// </summary>
        public string ModelName
        {
            get => ModelId;
            set => ModelId = value;
        }

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

        /// <summary>
        /// Total cost (compatibility property)
        /// </summary>
        public decimal TotalCost
        {
            get => Cost;
            set => Cost = value;
        }
    }
}
#pragma warning restore CS0618 // Type or member is obsolete