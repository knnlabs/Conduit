using ConduitLLM.WebUI.DTOs;
using System;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Service for retrieving cost dashboard data
    /// </summary>
    public interface ICostDashboardService
    {
        /// <summary>
        /// Gets cost dashboard data for a specified time period
        /// </summary>
        /// <param name="startDate">Start date of the period</param>
        /// <param name="endDate">End date of the period</param>
        /// <param name="virtualKeyId">Optional virtual key ID to filter by</param>
        /// <param name="modelName">Optional model name to filter by</param>
        /// <returns>Cost dashboard data</returns>
        Task<CostDashboardDto> GetCostDashboardDataAsync(
            DateTime startDate,
            DateTime endDate,
            int? virtualKeyId = null,
            string? modelName = null);
            
        /// <summary>
        /// Gets cost trend data for charting
        /// </summary>
        /// <param name="period">Period type (day, week, month)</param>
        /// <param name="count">Number of periods to include</param>
        /// <param name="virtualKeyId">Optional virtual key ID to filter by</param>
        /// <param name="modelName">Optional model name to filter by</param>
        /// <returns>Cost trend data</returns>
        Task<CostTrendDto> GetCostTrendAsync(
            string period,
            int count,
            int? virtualKeyId = null,
            string? modelName = null);
    }
}
