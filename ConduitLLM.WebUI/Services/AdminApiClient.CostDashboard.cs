using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs.Costs;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.WebUI.Interfaces;

namespace ConduitLLM.WebUI.Services
{
    public partial class AdminApiClient : ICostDashboardService
    {
        #region ICostDashboardService Implementation

        /// <inheritdoc />
        async Task<CostDashboardDto> ICostDashboardService.GetDashboardDataAsync(
            DateTime? startDate,
            DateTime? endDate,
            int? virtualKeyId,
            string? modelName)
        {
            try
            {
                // Apply default dates if not provided
                startDate ??= DateTime.UtcNow.AddDays(-30);
                endDate ??= DateTime.UtcNow;

                var result = await GetCostDashboardAsync(startDate, endDate, virtualKeyId, modelName);

                if (result == null)
                {
                    _logger.LogWarning("No cost dashboard data found for the specified period and filters");
                    return new CostDashboardDto
                    {
                        TotalCost = 0,
                        Last24HoursCost = 0,
                        Last7DaysCost = 0,
                        Last30DaysCost = 0,
                        TimeFrame = "daily",
                        StartDate = startDate ?? DateTime.Now.AddDays(-30),
                        EndDate = endDate ?? DateTime.Now,
                        TopModelsBySpend = new List<DetailedCostDataDto>(),
                        TopProvidersBySpend = new List<DetailedCostDataDto>(),
                        TopVirtualKeysBySpend = new List<DetailedCostDataDto>()
                    };
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving cost dashboard data");

                // Return an empty dashboard rather than null
                return new CostDashboardDto
                {
                    TotalCost = 0,
                    Last24HoursCost = 0,
                    Last7DaysCost = 0,
                    Last30DaysCost = 0,
                    TimeFrame = "daily",
                    StartDate = startDate ?? DateTime.Now.AddDays(-30),
                    EndDate = endDate ?? DateTime.Now,
                    TopModelsBySpend = new List<DetailedCostDataDto>(),
                    TopProvidersBySpend = new List<DetailedCostDataDto>(),
                    TopVirtualKeysBySpend = new List<DetailedCostDataDto>()
                };
            }
        }

        /// <inheritdoc />
        async Task<List<VirtualKeyDto>> ICostDashboardService.GetVirtualKeysAsync()
        {
            try
            {
                var keys = await GetAllVirtualKeysAsync();
                return keys?.ToList() ?? new List<VirtualKeyDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving virtual keys");
                return new List<VirtualKeyDto>();
            }
        }

        /// <inheritdoc />
        async Task<List<string>> ICostDashboardService.GetAvailableModelsAsync()
        {
            try
            {
                var models = await GetDistinctModelsAsync();
                return models?.ToList() ?? new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving available models");
                return new List<string>();
            }
        }

        /// <inheritdoc />
        async Task<List<DetailedCostDataDto>> ICostDashboardService.GetDetailedCostDataAsync(
            DateTime? startDate,
            DateTime? endDate,
            int? virtualKeyId,
            string? modelName)
        {
            try
            {
                var webUiResult = await GetDetailedCostDataAsync(startDate, endDate, virtualKeyId, modelName);

                if (webUiResult == null)
                {
                    _logger.LogWarning("No detailed cost data found for the specified period and filters");
                    return new List<DetailedCostDataDto>();
                }

                // Convert WebUI DTOs to Configuration DTOs if needed
                var detailedCostData = new List<DetailedCostDataDto>();
                foreach (var item in webUiResult)
                {
                    detailedCostData.Add(new DetailedCostDataDto
                    {
                        Name = item.Name ?? string.Empty,
                        Cost = item.Cost,
                        Percentage = item.Percentage
                    });
                }

                return detailedCostData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving detailed cost data");
                return new List<DetailedCostDataDto>();
            }
        }

        /// <inheritdoc />
        async Task<CostDashboardDto> ICostDashboardService.GetTrendDataAsync(
            string period,
            int count,
            int? virtualKeyId,
            string? modelName)
        {
            var (startDate, endDate) = ((ICostDashboardService)this).CalculateDateRange(period, count);
            return await ((ICostDashboardService)this).GetDashboardDataAsync(startDate, endDate, virtualKeyId, modelName);
        }

        /// <inheritdoc />
        bool ICostDashboardService.IsValidPeriod(string period)
        {
            return string.Equals(period, "day", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(period, "week", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(period, "month", StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        bool ICostDashboardService.IsValidCount(int count)
        {
            return count > 0 && count <= 365;
        }

        /// <inheritdoc />
        (DateTime startDate, DateTime endDate) ICostDashboardService.CalculateDateRange(string period, int count)
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

        #endregion
    }
}
