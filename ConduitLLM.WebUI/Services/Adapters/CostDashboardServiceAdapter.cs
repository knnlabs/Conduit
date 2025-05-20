using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.WebUI.Interfaces;
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
        public async Task<ConfigDTO.CostDashboardDto> GetDashboardDataAsync(
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
                var costByVirtualKey = virtualKeyStats.ToList();

                // Create and return the dashboard
                return new ConfigDTO.CostDashboardDto
                {
                    StartDate = effectiveStartDate,
                    EndDate = effectiveEndDate,
                    TotalCost = totalCost,
                    TotalRequests = totalRequests,
                    TotalInputTokens = totalInputTokens,
                    TotalOutputTokens = totalOutputTokens,
                    CostTrends = costTrends,
                    CostByModel = costByModel,
                    CostByVirtualKey = costByVirtualKey
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
        public async Task<List<ConfigDTO.VirtualKey.VirtualKeyDto>> GetVirtualKeysAsync()
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
                return new List<ConfigDTO.VirtualKey.VirtualKeyDto>();
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
        public async Task<List<ConfigDTO.DetailedCostDataDto>> GetDetailedCostDataAsync(
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

                return detailedCostData ?? new List<ConfigDTO.DetailedCostDataDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting detailed cost data");
                return new List<ConfigDTO.DetailedCostDataDto>();
            }
        }

        private ConfigDTO.CostDashboardDto CreateEmptyDashboard(DateTime startDate, DateTime endDate)
        {
            return new ConfigDTO.CostDashboardDto
            {
                StartDate = startDate,
                EndDate = endDate,
                TotalCost = 0,
                TotalRequests = 0,
                TotalInputTokens = 0,
                TotalOutputTokens = 0,
                CostTrends = new List<ConfigDTO.CostTrendDataDto>(),
                CostByModel = new List<ConfigDTO.ModelCostDataDto>(),
                CostByVirtualKey = new List<ConfigDTO.VirtualKeyCostDataDto>()
            };
        }
    }
}