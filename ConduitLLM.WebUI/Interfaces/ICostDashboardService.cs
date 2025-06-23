using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Interfaces
{
    /// <summary>
    /// Service for retrieving cost dashboard data
    /// </summary>
    public interface ICostDashboardService
    {
        /// <summary>
        /// Gets dashboard data for the specified period
        /// </summary>
        /// <param name="startDate">Start date of the period</param>
        /// <param name="endDate">End date of the period</param>
        /// <param name="virtualKeyId">Optional virtual key ID to filter by</param>
        /// <param name="modelName">Optional model name to filter by</param>
        /// <returns>Cost dashboard data</returns>
        Task<ConduitLLM.Configuration.DTOs.Costs.CostDashboardDto> GetDashboardDataAsync(
            DateTime? startDate,
            DateTime? endDate,
            int? virtualKeyId = null,
            string? modelName = null);

        /// <summary>
        /// Gets trend data for the specified period type and count
        /// </summary>
        /// <param name="period">The period type (day, week, or month)</param>
        /// <param name="count">The number of periods to include</param>
        /// <param name="virtualKeyId">Optional virtual key ID to filter by</param>
        /// <param name="modelName">Optional model name to filter by</param>
        /// <returns>Cost dashboard data for the calculated period</returns>
        Task<ConduitLLM.Configuration.DTOs.Costs.CostDashboardDto> GetTrendDataAsync(
            string period,
            int count,
            int? virtualKeyId = null,
            string? modelName = null);

        /// <summary>
        /// Validates the period type for trend data
        /// </summary>
        /// <param name="period">The period type to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        bool IsValidPeriod(string period);

        /// <summary>
        /// Validates the count for trend data
        /// </summary>
        /// <param name="count">The count to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        bool IsValidCount(int count);

        /// <summary>
        /// Calculates date range based on period and count
        /// </summary>
        /// <param name="period">The period type (day, week, or month)</param>
        /// <param name="count">The number of periods</param>
        /// <returns>Tuple containing start and end dates</returns>
        (DateTime startDate, DateTime endDate) CalculateDateRange(string period, int count);

        /// <summary>
        /// Gets a list of available virtual keys
        /// </summary>
        /// <returns>List of virtual keys</returns>
        Task<List<ConduitLLM.Configuration.DTOs.VirtualKey.VirtualKeyDto>> GetVirtualKeysAsync();

        /// <summary>
        /// Gets a list of available models
        /// </summary>
        /// <returns>List of model names</returns>
        Task<List<string>> GetAvailableModelsAsync();

        /// <summary>
        /// Gets detailed cost data for export
        /// </summary>
        /// <param name="startDate">Start date of the period</param>
        /// <param name="endDate">End date of the period</param>
        /// <param name="virtualKeyId">Optional virtual key ID to filter by</param>
        /// <param name="modelName">Optional model name to filter by</param>
        /// <returns>Detailed cost data</returns>
        Task<List<ConduitLLM.Configuration.DTOs.Costs.DetailedCostDataDto>> GetDetailedCostDataAsync(
            DateTime? startDate,
            DateTime? endDate,
            int? virtualKeyId = null,
            string? modelName = null);
    }
}
