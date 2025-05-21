using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.DTOs.Costs;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.DTOs;
using Microsoft.Extensions.Logging;

// Alias namespaces to disambiguate types
using ConfigDTO = ConduitLLM.Configuration.DTOs;
using ConfigServiceDTOs = ConduitLLM.Configuration.Services.Dtos;

namespace ConduitLLM.WebUI.Services.Adapters
{
    /// <summary>
    /// Adapter that implements <see cref="ICostDashboardService"/> using the Admin API client.
    /// </summary>
    public class CostDashboardServiceAdapter : ICostDashboardService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<CostDashboardServiceAdapter> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CostDashboardServiceAdapter"/> class.
        /// </summary>
        /// <param name="adminApiClient">The Admin API client.</param>
        /// <param name="logger">The logger.</param>
        public CostDashboardServiceAdapter(
            IAdminApiClient adminApiClient,
            ILogger<CostDashboardServiceAdapter> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<ConduitLLM.Configuration.DTOs.Costs.CostDashboardDto> GetDashboardDataAsync(
            DateTime? startDate,
            DateTime? endDate,
            int? virtualKeyId = null,
            string? modelName = null)
        {
            try
            {
                // Determine the date range
                var effectiveStartDate = startDate ?? DateTime.UtcNow.AddDays(-30);
                var effectiveEndDate = endDate ?? DateTime.UtcNow;

                // Get daily usage statistics
                var usageStats = await _adminApiClient.GetDailyUsageStatsAsync(
                    effectiveStartDate, effectiveEndDate, virtualKeyId);
                
                if (usageStats == null || !usageStats.Any())
                {
                    return CreateEmptyDashboard(effectiveStartDate, effectiveEndDate);
                }

                // Calculate totals
                decimal totalCost = usageStats.Sum(s => s.Cost);
                int totalRequests = usageStats.Sum(s => s.RequestCount);
                int totalInputTokens = usageStats.Sum(s => s.InputTokens);
                int totalOutputTokens = usageStats.Sum(s => s.OutputTokens);

                // Group by date for cost trends
                var costTrends = usageStats
                    .GroupBy(s => s.Date.Date)
                    .Select(g => new ConfigDTO.CostTrendDataDto
                    {
                        Date = g.Key,
                        Cost = g.Sum(s => s.Cost),
                        RequestCount = g.Sum(s => s.RequestCount),
                        InputTokens = g.Sum(s => s.InputTokens),
                        OutputTokens = g.Sum(s => s.OutputTokens)
                    })
                    .OrderBy(t => t.Date)
                    .ToList();

                // Group by model for model costs
                var costByModel = usageStats
                    .GroupBy(s => s.ModelName)
                    .Select(g => new ConfigDTO.ModelCostDataDto
                    {
                        ModelName = g.Key,
                        Cost = g.Sum(s => s.Cost),
                        RequestCount = g.Sum(s => s.RequestCount),
                        InputTokens = g.Sum(s => s.InputTokens),
                        OutputTokens = g.Sum(s => s.OutputTokens)
                    })
                    .OrderByDescending(m => m.Cost)
                    .ToList();

                // Get Virtual Key usage statistics
                var virtualKeyStats = await _adminApiClient.GetVirtualKeyUsageStatisticsAsync(virtualKeyId);
                
                // Convert WebUI DTOs to Configuration DTOs
                var costByVirtualKey = virtualKeyStats.Select(dto => new ConfigDTO.VirtualKeyCostDataDto
                {
                    VirtualKeyId = dto.VirtualKeyId,
                    VirtualKeyName = dto.KeyName,
                    Cost = dto.Cost,
                    RequestCount = dto.RequestCount
                }).ToList();

                // Try to get the cost dashboard data from the Admin API first
                var dashboardData = await _adminApiClient.GetCostDashboardAsync(
                    effectiveStartDate,
                    effectiveEndDate,
                    virtualKeyId,
                    modelName);
                    
                // Return the data directly from the API if available
                if (dashboardData != null)
                {
                    return dashboardData;
                }
                
                // If not available, construct a basic version from our calculations
                return new ConfigDTO.Costs.CostDashboardDto
                {
                    StartDate = effectiveStartDate,
                    EndDate = effectiveEndDate,
                    TotalCost = totalCost,
                    TimeFrame = "custom",
                    // Convert model costs to the new format
                    TopModelsBySpend = costByModel.Select(m => new ConfigDTO.Costs.DetailedCostDataDto 
                    { 
                        Name = m.ModelName,
                        Cost = m.Cost,
                        Percentage = totalCost > 0 ? (m.Cost / totalCost) * 100 : 0
                    }).ToList(),
                    // Convert virtual key costs to the new format
                    TopVirtualKeysBySpend = costByVirtualKey.Select(k => new ConfigDTO.Costs.DetailedCostDataDto
                    {
                        Name = k.VirtualKeyName ?? "Unknown",
                        Cost = k.Cost,
                        Percentage = totalCost > 0 ? (k.Cost / totalCost) * 100 : 0
                    }).ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard data");
                return CreateEmptyDashboard(
                    startDate ?? DateTime.UtcNow.AddDays(-30),
                    endDate ?? DateTime.UtcNow);
            }
        }

        /// <inheritdoc />
        public async Task<List<ConduitLLM.Configuration.DTOs.VirtualKey.VirtualKeyDto>> GetVirtualKeysAsync()
        {
            try
            {
                // Now we can directly use the configuration DTOs
                var configVirtualKeyDtos = await _adminApiClient.GetAllVirtualKeysAsync();
                return configVirtualKeyDtos.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting virtual keys");
                return new List<ConduitLLM.Configuration.DTOs.VirtualKey.VirtualKeyDto>();
            }
        }

        /// <inheritdoc />
        public async Task<List<string>> GetAvailableModelsAsync()
        {
            try
            {
                var models = await _adminApiClient.GetDistinctModelsAsync();
                return models.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available models");
                return new List<string>();
            }
        }

        /// <inheritdoc />
        public async Task<List<ConduitLLM.Configuration.DTOs.Costs.DetailedCostDataDto>> GetDetailedCostDataAsync(
            DateTime? startDate,
            DateTime? endDate,
            int? virtualKeyId = null,
            string? modelName = null)
        {
            try
            {
                // Determine the date range
                var effectiveStartDate = startDate ?? DateTime.UtcNow.AddDays(-30);
                var effectiveEndDate = endDate ?? DateTime.UtcNow;

                // Get the detailed cost data
                var detailedCostData = await _adminApiClient.GetDetailedCostDataAsync(
                    effectiveStartDate, 
                    effectiveEndDate, 
                    virtualKeyId, 
                    modelName);

                // Convert the WebUI DTO to the Configuration DTO needed by the interface
                if (detailedCostData == null)
                {
                    return new List<ConduitLLM.Configuration.DTOs.Costs.DetailedCostDataDto>();
                }
                
                var result = new List<ConduitLLM.Configuration.DTOs.Costs.DetailedCostDataDto>();
                foreach (var dto in detailedCostData)
                {
                    // Convert WebUI DTO to Configuration DTO
                    result.Add(new ConduitLLM.Configuration.DTOs.Costs.DetailedCostDataDto
                    {
                        Name = dto.Name ?? "Unknown",
                        Cost = dto.Cost,
                        Percentage = dto.Percentage
                    });
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting detailed cost data");
                return new List<ConduitLLM.Configuration.DTOs.Costs.DetailedCostDataDto>();
            }
        }

        private ConfigDTO.Costs.CostDashboardDto CreateEmptyDashboard(DateTime startDate, DateTime endDate)
        {
            return new ConfigDTO.Costs.CostDashboardDto
            {
                StartDate = startDate,
                EndDate = endDate,
                TotalCost = 0,
                TimeFrame = "custom",
                Last24HoursCost = 0,
                Last7DaysCost = 0,
                Last30DaysCost = 0,
                TopModelsBySpend = new List<ConfigDTO.Costs.DetailedCostDataDto>(),
                TopProvidersBySpend = new List<ConfigDTO.Costs.DetailedCostDataDto>(),
                TopVirtualKeysBySpend = new List<ConfigDTO.Costs.DetailedCostDataDto>()
            };
        }
    }
}