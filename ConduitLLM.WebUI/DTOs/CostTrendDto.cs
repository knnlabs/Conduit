using System;
using System.Collections.Generic;

namespace ConduitLLM.WebUI.DTOs
{
    /// <summary>
    /// Data transfer object for cost trend data
    /// </summary>
    public class CostTrendDto
    {
        /// <summary>
        /// Period type (day, week, month)
        /// </summary>
        public string PeriodType { get; set; } = string.Empty;

        /// <summary>
        /// Number of periods included
        /// </summary>
        public int PeriodCount { get; set; }

        /// <summary>
        /// Start date of the entire period
        /// </summary>
        public DateTime StartDate { get; set; }

        /// <summary>
        /// End date of the entire period
        /// </summary>
        public DateTime EndDate { get; set; }

        /// <summary>
        /// List of periods with cost data
        /// </summary>
        public List<PeriodCostData> Periods { get; set; } = new List<PeriodCostData>();

        /// <summary>
        /// Total cost across all periods
        /// </summary>
        public decimal TotalCost { get; set; }

        /// <summary>
        /// Total requests across all periods
        /// </summary>
        public int TotalRequests { get; set; }

        /// <summary>
        /// Optional filter applied (virtual key ID)
        /// </summary>
        public int? VirtualKeyId { get; set; }

        /// <summary>
        /// Optional filter applied (model name)
        /// </summary>
        public string? ModelName { get; set; }
    }

    /// <summary>
    /// Cost data for a specific period
    /// </summary>
    public class PeriodCostData
    {
        /// <summary>
        /// Label for the period (e.g., "Jan 2025" or "Week 12")
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Start date of the period
        /// </summary>
        public DateTime StartDate { get; set; }

        /// <summary>
        /// End date of the period
        /// </summary>
        public DateTime EndDate { get; set; }

        /// <summary>
        /// Total cost for the period
        /// </summary>
        public decimal Cost { get; set; }

        /// <summary>
        /// Number of requests in the period
        /// </summary>
        public int RequestCount { get; set; }

        /// <summary>
        /// Input tokens in the period
        /// </summary>
        public int InputTokens { get; set; }

        /// <summary>
        /// Output tokens in the period
        /// </summary>
        public int OutputTokens { get; set; }

        /// <summary>
        /// Cost breakdown by model for this period
        /// </summary>
        public Dictionary<string, decimal> CostByModel { get; set; } = new Dictionary<string, decimal>();

        /// <summary>
        /// Cost breakdown by key for this period
        /// </summary>
        public Dictionary<int, KeyPeriodData> CostByKey { get; set; } = new Dictionary<int, KeyPeriodData>();
    }

    /// <summary>
    /// Key data for a specific period
    /// </summary>
    public class KeyPeriodData
    {
        /// <summary>
        /// Key name
        /// </summary>
        public string KeyName { get; set; } = string.Empty;

        /// <summary>
        /// Cost for this key in this period
        /// </summary>
        public decimal Cost { get; set; }
    }
}
