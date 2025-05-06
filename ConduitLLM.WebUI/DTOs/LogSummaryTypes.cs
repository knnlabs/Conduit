using System;
using System.Collections.Generic;

namespace ConduitLLM.WebUI.DTOs
{
    /// <summary>
    /// Summary of usage statistics for a specific virtual key
    /// </summary>
    public class KeyUsageSummary
    {
        /// <summary>
        /// ID of the virtual key
        /// </summary>
        public int VirtualKeyId { get; set; }
        
        /// <summary>
        /// Name of the virtual key
        /// </summary>
        public string KeyName { get; set; } = string.Empty;
        
        /// <summary>
        /// Total number of requests made with this key
        /// </summary>
        public int TotalRequests { get; set; }
        
        /// <summary>
        /// Total cost incurred by this key
        /// </summary>
        public decimal TotalCost { get; set; }
        
        /// <summary>
        /// Average response time in milliseconds
        /// </summary>
        public double AverageResponseTimeMs { get; set; }
        
        /// <summary>
        /// Average response time (alias for AverageResponseTimeMs)
        /// </summary>
        public double AverageResponseTime { get; set; }
        
        /// <summary>
        /// Total input tokens used
        /// </summary>
        public int TotalInputTokens { get; set; }
        
        /// <summary>
        /// Total output tokens generated
        /// </summary>
        public int TotalOutputTokens { get; set; }
        
        /// <summary>
        /// Date of the first request
        /// </summary>
        public DateTime FirstRequestDate { get; set; }
        
        /// <summary>
        /// First request time (alias for FirstRequestDate)
        /// </summary>
        public DateTime FirstRequestTime { get; set; }
        
        /// <summary>
        /// Date of the most recent request
        /// </summary>
        public DateTime LastRequestDate { get; set; }
        
        /// <summary>
        /// Last request time (alias for LastRequestDate)
        /// </summary>
        public DateTime LastRequestTime { get; set; }
        
        /// <summary>
        /// Request counts by model
        /// </summary>
        public Dictionary<string, int> RequestsByModel { get; set; } = new Dictionary<string, int>();
        
        /// <summary>
        /// Success rate as a percentage
        /// </summary>
        public double SuccessRate { get; set; }
        
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
    
    /// <summary>
    /// Aggregated usage summary for a virtual key
    /// </summary>
    public class KeyAggregateSummary
    {
        /// <summary>
        /// ID of the virtual key
        /// </summary>
        public int VirtualKeyId { get; set; }
        
        /// <summary>
        /// Name of the virtual key
        /// </summary>
        public string KeyName { get; set; } = string.Empty;
        
        /// <summary>
        /// Total number of requests
        /// </summary>
        public int RequestCount { get; set; }
        
        /// <summary>
        /// Total number of requests (alias for RequestCount)
        /// </summary>
        public int TotalRequests { get; set; }
        
        /// <summary>
        /// Total cost incurred
        /// </summary>
        public decimal TotalCost { get; set; }
        
        /// <summary>
        /// Date of the last request
        /// </summary>
        public DateTime LastUsed { get; set; }
        
        /// <summary>
        /// Whether the key is currently enabled
        /// </summary>
        public bool IsActive { get; set; }
        
        /// <summary>
        /// Budget information for the key
        /// </summary>
        public decimal? BudgetLimit { get; set; }
        
        /// <summary>
        /// Current spend against the budget
        /// </summary>
        public decimal CurrentSpend { get; set; }
        
        /// <summary>
        /// Budget period (daily, monthly, total)
        /// </summary>
        public string? BudgetPeriod { get; set; }
        
        /// <summary>
        /// Percentage of budget used
        /// </summary>
        public double BudgetUsedPercentage => BudgetLimit.HasValue && BudgetLimit.Value > 0 
            ? Math.Min(100, (double)(CurrentSpend / BudgetLimit.Value * 100M))
            : 0;
        
        /// <summary>
        /// Average response time in milliseconds
        /// </summary>
        public double AverageResponseTime { get; set; }
        
        /// <summary>
        /// Recent requests (in the last 24 hours)
        /// </summary>
        public int RecentRequests { get; set; }
    }
    
    /// <summary>
    /// Daily usage statistics
    /// </summary>
    public class DailyUsageSummary
    {
        /// <summary>
        /// The date for these statistics
        /// </summary>
        public DateTime Date { get; set; }
        
        /// <summary>
        /// Total number of requests for this day
        /// </summary>
        public int RequestCount { get; set; }
        
        /// <summary>
        /// Total cost for this day
        /// </summary>
        public decimal TotalCost { get; set; }
        
        /// <summary>
        /// Total input tokens for this day
        /// </summary>
        public int TotalInputTokens { get; set; }
        
        /// <summary>
        /// Input tokens (alias for TotalInputTokens)
        /// </summary>
        public int InputTokens { get; set; }
        
        /// <summary>
        /// Total output tokens for this day
        /// </summary>
        public int TotalOutputTokens { get; set; }
        
        /// <summary>
        /// Output tokens (alias for TotalOutputTokens)
        /// </summary>
        public int OutputTokens { get; set; }
        
        /// <summary>
        /// Success rate as a percentage
        /// </summary>
        public double SuccessRate { get; set; }
    }
}