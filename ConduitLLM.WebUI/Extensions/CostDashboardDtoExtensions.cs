using System;
using System.Collections.Generic;
using System.Linq;

using ConduitLLM.Configuration.DTOs.Costs;

namespace ConduitLLM.WebUI.Extensions
{
    /// <summary>
    /// Extension methods for the CostDashboardDto class
    /// </summary>
    public static class CostDashboardDtoExtensions
    {
        // Extension properties to be accessed in CostDashboard.razor
        private static int totalRequestsValue = 0;

        // Extension method to get TotalRequests 
        public static int TotalRequests(this CostDashboardDto dto) => totalRequestsValue;

        // Extension methods for collection properties
        private static List<CostTrendDataDto> DefaultCostTrends() => new();

        public static List<CostTrendDataDto> CostTrends(this CostDashboardDto dto) =>
            DefaultCostTrends();

        private static Dictionary<string, decimal> DefaultCostByModel() => new();

        public static Dictionary<string, decimal> CostByModel(this CostDashboardDto dto)
        {
            var result = new Dictionary<string, decimal>();
            foreach (var model in dto.TopModelsBySpend)
            {
                result[model.Name] = model.Cost;
            }
            return result;
        }

        private static Dictionary<int, KeyCostData> DefaultCostByVirtualKey() => new();

        public static Dictionary<int, KeyCostData> CostByVirtualKey(this CostDashboardDto dto) =>
            DefaultCostByVirtualKey();
    }

    /// <summary>
    /// Temporary model for cost trend data 
    /// </summary>
    public class CostTrendDataDto
    {
        /// <summary>
        /// Date of the trend point
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Cost for this point
        /// </summary>
        public decimal Cost { get; set; }
    }

    /// <summary>
    /// Temporary model for key cost data
    /// </summary>
    public class KeyCostData
    {
        /// <summary>
        /// Name of the key
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Cost for this key
        /// </summary>
        public decimal Cost { get; set; }
    }
}
