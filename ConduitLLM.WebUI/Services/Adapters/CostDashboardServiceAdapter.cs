using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs.Costs;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.WebUI.Interfaces;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services.Adapters
{
    /// <summary>
    /// Adapter that bridges the ICostDashboardService interface with the Admin API client
    /// </summary>
    public class CostDashboardServiceAdapter : ICostDashboardService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<CostDashboardServiceAdapter> _logger;

        public CostDashboardServiceAdapter(IAdminApiClient adminApiClient, ILogger<CostDashboardServiceAdapter> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<CostDashboardDto> GetDashboardDataAsync(
            DateTime? startDate,
            DateTime? endDate,
            int? virtualKeyId = null,
            string? modelName = null)
        {
            try
            {
                // Apply default dates if not provided
                startDate ??= DateTime.UtcNow.AddDays(-30);
                endDate ??= DateTime.UtcNow;

                // Get dashboard data from Admin API
                var dashboardData = await _adminApiClient.GetCostDashboardAsync(startDate, endDate, virtualKeyId, modelName);
                
                if (dashboardData != null)
                {
                    return dashboardData;
                }

                // If no data from the API, create an empty dashboard based on usage statistics
                var start = startDate ?? DateTime.UtcNow.AddDays(-30);
                var end = endDate ?? DateTime.UtcNow;

                // Get daily usage stats to build dashboard
                var usageStats = await _adminApiClient.GetDailyUsageStatsAsync(start, end, virtualKeyId);
                var virtualKeyStats = await _adminApiClient.GetVirtualKeyUsageStatisticsAsync(virtualKeyId);

                // Create a dashboard DTO from the available data
                var dashboard = new CostDashboardDto
                {
                    StartDate = start,
                    EndDate = end,
                    TotalCost = 0m,
                    TimeFrame = "daily",
                    TopModelsBySpend = new List<DetailedCostDataDto>(),
                    TopProvidersBySpend = new List<DetailedCostDataDto>(),
                    TopVirtualKeysBySpend = new List<DetailedCostDataDto>()
                };

                // Calculate costs for different periods
                var now = DateTime.UtcNow;
                var last24Hours = now.AddHours(-24);
                var last7Days = now.AddDays(-7);
                var last30Days = now.AddDays(-30);

                // Aggregate data from usage stats
                if (usageStats != null && usageStats.Any())
                {
                    // Calculate total cost
                    dashboard.TotalCost = usageStats.Sum(s => s.TotalCost);
                    
                    // Calculate period costs
                    dashboard.Last24HoursCost = usageStats
                        .Where(s => s.Date >= last24Hours)
                        .Sum(s => s.TotalCost);
                    
                    dashboard.Last7DaysCost = usageStats
                        .Where(s => s.Date >= last7Days)
                        .Sum(s => s.TotalCost);
                    
                    dashboard.Last30DaysCost = usageStats
                        .Where(s => s.Date >= last30Days)
                        .Sum(s => s.TotalCost);

                    // Get top models by spend
                    var modelCosts = usageStats
                        .GroupBy(s => s.ModelName)
                        .Select(g => new
                        {
                            Name = g.Key,
                            Cost = g.Sum(s => s.TotalCost)
                        })
                        .OrderByDescending(m => m.Cost)
                        .Take(10)
                        .ToList();

                    dashboard.TopModelsBySpend = modelCosts
                        .Select(m => new DetailedCostDataDto
                        {
                            Name = m.Name,
                            Cost = m.Cost,
                            Percentage = dashboard.TotalCost > 0 ? Math.Round((m.Cost / dashboard.TotalCost) * 100, 2) : 0
                        })
                        .ToList();
                }

                // Add virtual key costs if available
                if (virtualKeyStats != null && virtualKeyStats.Any())
                {
                    dashboard.TopVirtualKeysBySpend = virtualKeyStats
                        .OrderByDescending(v => v.TotalCost)
                        .Take(10)
                        .Select(v => new DetailedCostDataDto
                        {
                            Name = v.VirtualKeyName ?? $"Key {v.VirtualKeyId}",
                            Cost = v.TotalCost,
                            Percentage = dashboard.TotalCost > 0 ? Math.Round((v.TotalCost / dashboard.TotalCost) * 100, 2) : 0
                        })
                        .ToList();

                    // Update total cost if not already calculated
                    if (dashboard.TotalCost == 0m)
                    {
                        dashboard.TotalCost = virtualKeyStats.Sum(v => v.TotalCost);
                    }
                }

                return dashboard;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard data");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<VirtualKeyDto>> GetVirtualKeysAsync()
        {
            var keys = await _adminApiClient.GetAllVirtualKeysAsync();
            return keys?.ToList() ?? new List<VirtualKeyDto>();
        }

        /// <inheritdoc />
        public async Task<List<string>> GetAvailableModelsAsync()
        {
            var models = await _adminApiClient.GetDistinctModelsAsync();
            return models?.ToList() ?? new List<string>();
        }

        /// <inheritdoc />
        public async Task<List<DetailedCostDataDto>> GetDetailedCostDataAsync(
            DateTime? startDate,
            DateTime? endDate,
            int? virtualKeyId = null,
            string? modelName = null)
        {
            // Check if the admin API has a GetDetailedCostDataAsync method
            // If not, we'll need to build it from other data
            var webUIDetailedData = await _adminApiClient.GetDetailedCostDataAsync(startDate, endDate, virtualKeyId, modelName);
            
            if (webUIDetailedData != null)
            {
                // Convert WebUI DTOs to Configuration DTOs
                return webUIDetailedData.Select(d => new DetailedCostDataDto
                {
                    Name = d.Name,
                    Cost = d.Cost,
                    Percentage = d.Percentage
                }).ToList();
            }

            return new List<DetailedCostDataDto>();
        }

        /// <inheritdoc />
        public async Task<CostDashboardDto> GetTrendDataAsync(
            string period,
            int count,
            int? virtualKeyId = null,
            string? modelName = null)
        {
            var (startDate, endDate) = CalculateDateRange(period, count);
            return await GetDashboardDataAsync(startDate, endDate, virtualKeyId, modelName);
        }

        /// <inheritdoc />
        public bool IsValidPeriod(string period)
        {
            return string.Equals(period, "day", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(period, "week", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(period, "month", StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        public bool IsValidCount(int count)
        {
            return count > 0 && count <= 365;
        }

        /// <inheritdoc />
        public (DateTime startDate, DateTime endDate) CalculateDateRange(string period, int count)
        {
            var endDate = DateTime.UtcNow;
            DateTime startDate;

            switch (period.ToLower())
            {
                case "week":
                    startDate = endDate.AddDays(-7 * count);
                    break;
                case "month":
                    startDate = endDate.AddMonths(-count);
                    break;
                case "day":
                default:
                    startDate = endDate.AddDays(-count);
                    break;
            }

            return (startDate, endDate);
        }
    }
}