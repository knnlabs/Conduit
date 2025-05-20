using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.DTOs.Costs
{
    /// <summary>
    /// Data transfer object for cost dashboard statistics
    /// </summary>
    public class CostDashboardDto
    {
        /// <summary>
        /// Time frame of the data (daily, weekly, monthly)
        /// </summary>
        public string TimeFrame { get; set; } = "daily";
        
        /// <summary>
        /// Start date of the period
        /// </summary>
        public DateTime StartDate { get; set; }
        
        /// <summary>
        /// End date of the period
        /// </summary>
        public DateTime EndDate { get; set; }
        
        /// <summary>
        /// Cost in the last 24 hours
        /// </summary>
        public decimal Last24HoursCost { get; set; }
        
        /// <summary>
        /// Cost in the last 7 days
        /// </summary>
        public decimal Last7DaysCost { get; set; }
        
        /// <summary>
        /// Cost in the last 30 days
        /// </summary>
        public decimal Last30DaysCost { get; set; }
        
        /// <summary>
        /// Total cost across all requests in the period
        /// </summary>
        public decimal TotalCost { get; set; }
        
        /// <summary>
        /// Top models by spend
        /// </summary>
        public List<DetailedCostDataDto> TopModelsBySpend { get; set; } = new List<DetailedCostDataDto>();
        
        /// <summary>
        /// Top providers by spend
        /// </summary>
        public List<DetailedCostDataDto> TopProvidersBySpend { get; set; } = new List<DetailedCostDataDto>();
        
        /// <summary>
        /// Top virtual keys by spend
        /// </summary>
        public List<DetailedCostDataDto> TopVirtualKeysBySpend { get; set; } = new List<DetailedCostDataDto>();
    }
}