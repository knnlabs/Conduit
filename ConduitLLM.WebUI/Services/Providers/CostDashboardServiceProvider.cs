using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.Extensions.Logging;
using CostDTOs = ConduitLLM.Configuration.DTOs.Costs;

namespace ConduitLLM.WebUI.Services.Providers
{
    /// <summary>
    /// Implementation of ICostDashboardService that uses IAdminApiClient to interact with the Admin API
    /// </summary>
    public class CostDashboardServiceProvider : ICostDashboardService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<CostDashboardServiceProvider> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CostDashboardServiceProvider"/> class.
        /// </summary>
        /// <param name="adminApiClient">The Admin API client.</param>
        /// <param name="logger">The logger.</param>
        public CostDashboardServiceProvider(
            IAdminApiClient adminApiClient,
            ILogger<CostDashboardServiceProvider> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<CostDTOs.CostDashboardDto> GetDashboardDataAsync(
            DateTime? startDate, 
            DateTime? endDate, 
            int? virtualKeyId = null, 
            string? modelName = null)
        {
            try
            {
                // Set default dates if not provided
                startDate ??= DateTime.UtcNow.AddDays(-30);
                endDate ??= DateTime.UtcNow;

                // Get dashboard data from Admin API
                var dashboardData = await _adminApiClient.GetCostDashboardAsync(
                    startDate,
                    endDate,
                    virtualKeyId,
                    modelName);

                if (dashboardData == null)
                {
                    _logger.LogWarning("Failed to retrieve cost dashboard data from Admin API");
                    
                    // Return an empty dashboard
                    return new CostDTOs.CostDashboardDto
                    {
                        TotalCost = 0,
                        StartDate = startDate.Value,
                        EndDate = endDate.Value,
                        Last24HoursCost = 0,
                        Last7DaysCost = 0,
                        Last30DaysCost = 0,
                        TopModelsBySpend = new List<CostDTOs.DetailedCostDataDto>(),
                        TopProvidersBySpend = new List<CostDTOs.DetailedCostDataDto>(),
                        TopVirtualKeysBySpend = new List<CostDTOs.DetailedCostDataDto>()
                    };
                }

                return dashboardData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving cost dashboard data from Admin API");
                
                // Return an empty dashboard in case of error
                return new CostDTOs.CostDashboardDto
                {
                    TotalCost = 0,
                    StartDate = startDate ?? DateTime.UtcNow.AddDays(-30),
                    EndDate = endDate ?? DateTime.UtcNow,
                    Last24HoursCost = 0,
                    Last7DaysCost = 0,
                    Last30DaysCost = 0,
                    TopModelsBySpend = new List<CostDTOs.DetailedCostDataDto>(),
                    TopProvidersBySpend = new List<CostDTOs.DetailedCostDataDto>(),
                    TopVirtualKeysBySpend = new List<CostDTOs.DetailedCostDataDto>()
                };
            }
        }

        /// <inheritdoc />
        public async Task<List<VirtualKeyDto>> GetVirtualKeysAsync()
        {
            try
            {
                // Get all virtual keys from Admin API
                var keys = await _adminApiClient.GetAllVirtualKeysAsync();
                return keys != null ? new List<VirtualKeyDto>(keys) : new List<VirtualKeyDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving virtual keys from Admin API");
                return new List<VirtualKeyDto>();
            }
        }

        /// <inheritdoc />
        public async Task<List<string>> GetAvailableModelsAsync()
        {
            try
            {
                // Get distinct models from Admin API
                var models = await _adminApiClient.GetDistinctModelsAsync();
                return models != null ? new List<string>(models) : new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving available models from Admin API");
                return new List<string>();
            }
        }

        /// <inheritdoc />
        public async Task<List<CostDTOs.DetailedCostDataDto>> GetDetailedCostDataAsync(
            DateTime? startDate, 
            DateTime? endDate, 
            int? virtualKeyId = null, 
            string? modelName = null)
        {
            try
            {
                // Set default dates if not provided
                startDate ??= DateTime.UtcNow.AddDays(-30);
                endDate ??= DateTime.UtcNow;

                // Get detailed cost data from Admin API
                var detailedData = await _adminApiClient.GetDetailedCostDataAsync(
                    startDate,
                    endDate,
                    virtualKeyId,
                    modelName);

                if (detailedData == null)
                {
                    _logger.LogWarning("Failed to retrieve detailed cost data from Admin API");
                    return new List<CostDTOs.DetailedCostDataDto>();
                }

                // Convert the WebUI DTOs to Configuration.DTOs.Costs DTOs
                var result = new List<CostDTOs.DetailedCostDataDto>();
                foreach (var item in detailedData)
                {
                    result.Add(new CostDTOs.DetailedCostDataDto
                    {
                        Name = item.Name ?? "",
                        Cost = item.Cost,
                        Percentage = item.Percentage
                    });
                }

                // Return the detailed cost data
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving detailed cost data from Admin API");
                return new List<CostDTOs.DetailedCostDataDto>();
            }
        }
    }
}