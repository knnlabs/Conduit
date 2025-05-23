using ConduitLLM.Configuration.DTOs.Costs;

namespace ConduitLLM.Admin.Interfaces;

/// <summary>
/// Service interface for cost dashboard functionality through the Admin API
/// </summary>
public interface IAdminCostDashboardService
{
    /// <summary>
    /// Gets cost dashboard summary data
    /// </summary>
    /// <param name="timeframe">The timeframe for the summary (daily, weekly, monthly)</param>
    /// <param name="startDate">The start date for the summary</param>
    /// <param name="endDate">The end date for the summary</param>
    /// <returns>The cost dashboard summary</returns>
    Task<CostDashboardDto> GetCostSummaryAsync(
        string timeframe = "daily", 
        DateTime? startDate = null, 
        DateTime? endDate = null);
    
    /// <summary>
    /// Gets cost trend data
    /// </summary>
    /// <param name="period">The period for the trend (daily, weekly, monthly)</param>
    /// <param name="startDate">The start date for the trend</param>
    /// <param name="endDate">The end date for the trend</param>
    /// <returns>The cost trend data</returns>
    Task<CostTrendDto> GetCostTrendsAsync(
        string period = "daily", 
        DateTime? startDate = null, 
        DateTime? endDate = null);
    
    /// <summary>
    /// Gets model costs data
    /// </summary>
    /// <param name="startDate">The start date for the data</param>
    /// <param name="endDate">The end date for the data</param>
    /// <returns>The model costs data</returns>
    Task<List<ModelCostDataDto>> GetModelCostsAsync(
        DateTime? startDate = null, 
        DateTime? endDate = null);
    
    /// <summary>
    /// Gets virtual key costs data
    /// </summary>
    /// <param name="startDate">The start date for the data</param>
    /// <param name="endDate">The end date for the data</param>
    /// <returns>The virtual key costs data</returns>
    Task<List<VirtualKeyCostDataDto>> GetVirtualKeyCostsAsync(
        DateTime? startDate = null, 
        DateTime? endDate = null);
}