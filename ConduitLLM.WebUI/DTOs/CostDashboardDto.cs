using System;
using System.Collections.Generic;

namespace ConduitLLM.WebUI.DTOs
{
    /// <summary>
    /// Data transfer object for cost dashboard statistics
    /// </summary>
    public class CostDashboardDto
    {
        /// <summary>
        /// Start date of the period
        /// </summary>
        public DateTime StartDate { get; set; }
        
        /// <summary>
        /// End date of the period
        /// </summary>
        public DateTime EndDate { get; set; }
        
        /// <summary>
        /// Total cost across all requests in the period
        /// </summary>
        public decimal TotalCost { get; set; }
        
        /// <summary>
        /// Total number of requests in the period
        /// </summary>
        public int TotalRequests { get; set; }
        
        /// <summary>
        /// Total input tokens across all requests
        /// </summary>
        public int TotalInputTokens { get; set; }
        
        /// <summary>
        /// Total output tokens across all requests
        /// </summary>
        public int TotalOutputTokens { get; set; }
        
        /// <summary>
        /// Cost by day for the period
        /// </summary>
        public Dictionary<DateTime, decimal> CostByDay { get; set; } = new Dictionary<DateTime, decimal>();
        
        /// <summary>
        /// Cost by model for the period
        /// </summary>
        public Dictionary<string, decimal> CostByModel { get; set; } = new Dictionary<string, decimal>();
        
        /// <summary>
        /// Cost by virtual key for the period
        /// </summary>
        public Dictionary<int, KeyCostData> CostByKey { get; set; } = new Dictionary<int, KeyCostData>();
        
        /// <summary>
        /// Requests by day for the period
        /// </summary>
        public Dictionary<DateTime, int> RequestsByDay { get; set; } = new Dictionary<DateTime, int>();
        
        /// <summary>
        /// Tokens by day for the period
        /// </summary>
        public Dictionary<DateTime, TokenData> TokensByDay { get; set; } = new Dictionary<DateTime, TokenData>();
        
        /// <summary>
        /// Average cost per request
        /// </summary>
        public decimal AverageCostPerRequest => TotalRequests > 0 ? TotalCost / TotalRequests : 0;
        
        /// <summary>
        /// Average cost per 1000 tokens
        /// </summary>
        public decimal AverageCostPer1000Tokens => (TotalInputTokens + TotalOutputTokens) > 0 
            ? TotalCost / ((TotalInputTokens + TotalOutputTokens) / 1000.0m) 
            : 0;
    }
    
    /// <summary>
    /// Data about token counts
    /// </summary>
    public class TokenData
    {
        /// <summary>
        /// Input tokens
        /// </summary>
        public int InputTokens { get; set; }
        
        /// <summary>
        /// Output tokens
        /// </summary>
        public int OutputTokens { get; set; }
        
        /// <summary>
        /// Total tokens (input + output)
        /// </summary>
        public int TotalTokens => InputTokens + OutputTokens;
    }
    
    /// <summary>
    /// Data about a virtual key's cost
    /// </summary>
    public class KeyCostData
    {
        /// <summary>
        /// Name of the virtual key
        /// </summary>
        public string KeyName { get; set; } = string.Empty;
        
        /// <summary>
        /// Total cost for this key
        /// </summary>
        public decimal TotalCost { get; set; }
        
        /// <summary>
        /// Request count for this key
        /// </summary>
        public int RequestCount { get; set; }
        
        /// <summary>
        /// Input tokens for this key
        /// </summary>
        public int InputTokens { get; set; }
        
        /// <summary>
        /// Output tokens for this key
        /// </summary>
        public int OutputTokens { get; set; }
        
        /// <summary>
        /// Maximum budget for this key, if any
        /// </summary>
        public decimal? MaxBudget { get; set; }
        
        /// <summary>
        /// Percentage of budget used
        /// </summary>
        public decimal BudgetUsedPercentage => MaxBudget.HasValue && MaxBudget > 0 
            ? Math.Min(100, (TotalCost / MaxBudget.Value) * 100) 
            : 0;
    }
}
