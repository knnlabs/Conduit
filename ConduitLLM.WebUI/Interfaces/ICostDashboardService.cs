using ConduitLLM.Configuration.Entities;
using ConduitLLM.WebUI.DTOs;
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
        Task<CostDashboardDto> GetDashboardDataAsync(
            DateTime? startDate,
            DateTime? endDate,
            int? virtualKeyId = null,
            string? modelName = null);
            
        /// <summary>
        /// Gets a list of available virtual keys
        /// </summary>
        /// <returns>List of virtual keys</returns>
        Task<List<VirtualKey>> GetVirtualKeysAsync();
        
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
        Task<List<DetailedCostDataDto>> GetDetailedCostDataAsync(
            DateTime? startDate,
            DateTime? endDate,
            int? virtualKeyId = null,
            string? modelName = null);
    }
}