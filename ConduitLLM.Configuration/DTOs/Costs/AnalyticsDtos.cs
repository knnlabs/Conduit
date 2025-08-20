using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.DTOs.Costs
{
    /// <summary>
    /// Model cost breakdown DTO
    /// </summary>
    public class ModelCostBreakdownDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<ModelCostDetail> Models { get; set; } = new();
        public decimal TotalCost { get; set; }
        public int TotalRequests { get; set; }
    }

    /// <summary>
    /// Virtual key cost breakdown DTO
    /// </summary>
    public class VirtualKeyCostBreakdownDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<VirtualKeyCostDetail> VirtualKeys { get; set; } = new();
        public decimal TotalCost { get; set; }
        public int TotalRequests { get; set; }
    }

    /// <summary>
    /// Model cost detail
    /// </summary>
    public class ModelCostDetail
    {
        public string ModelName { get; set; } = string.Empty;
        public decimal TotalCost { get; set; }
        public int RequestCount { get; set; }
        public long InputTokens { get; set; }
        public long OutputTokens { get; set; }
        public decimal AverageCostPerRequest { get; set; }
        public decimal CostPercentage { get; set; }
    }

    /// <summary>
    /// Virtual key cost detail
    /// </summary>
    public class VirtualKeyCostDetail
    {
        public int VirtualKeyId { get; set; }
        public string KeyName { get; set; } = string.Empty;
        public decimal TotalCost { get; set; }
        public int RequestCount { get; set; }
        public decimal AverageCostPerRequest { get; set; }
        public DateTime? LastUsed { get; set; }
        public int UniqueModels { get; set; }
    }

    /// <summary>
    /// Provider cost detail
    /// </summary>
    public class ProviderCostDetail
    {
        public string ProviderName { get; set; } = string.Empty;
        public decimal TotalCost { get; set; }
        public int RequestCount { get; set; }
        public decimal AverageCostPerRequest { get; set; }
        public decimal CostPercentage { get; set; }
    }

    /// <summary>
    /// Cost trend point
    /// </summary>
    public class CostTrendPoint
    {
        public DateTime Date { get; set; }
        public decimal Cost { get; set; }
        public int RequestCount { get; set; }
        public decimal AverageRequestCost { get; set; }
    }
}