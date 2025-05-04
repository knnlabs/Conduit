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
        /// Cost trend data for daily costs
        /// </summary>
        public List<CostTrendDataDto> CostTrends { get; set; } = new List<CostTrendDataDto>();
        
        /// <summary>
        /// Cost data by model
        /// </summary>
        public List<ModelCostDataDto> CostByModel { get; set; } = new List<ModelCostDataDto>();
        
        /// <summary>
        /// Cost data by virtual key
        /// </summary>
        public List<VirtualKeyCostDataDto> CostByVirtualKey { get; set; } = new List<VirtualKeyCostDataDto>();
    }
}