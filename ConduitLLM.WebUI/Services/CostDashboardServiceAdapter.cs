using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Adapter service for cost dashboard data that uses the Admin API
    /// </summary>
    public class CostDashboardServiceAdapter : ICostDashboardService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<CostDashboardServiceAdapter> _logger;
        
        /// <summary>
        /// Initializes a new instance of the CostDashboardServiceAdapter class
        /// </summary>
        /// <param name="adminApiClient">The Admin API client</param>
        /// <param name="logger">The logger</param>
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
                // The Admin API client needs to provide a similar method to fetch cost dashboard data
                var configDto = await _adminApiClient.GetCostDashboardAsync(startDate, endDate, virtualKeyId, modelName);

                if (configDto == null)
                {
                    return new ConduitLLM.Configuration.DTOs.Costs.CostDashboardDto
                    {
                        StartDate = startDate.GetValueOrDefault(DateTime.UtcNow.AddDays(-7)),
                        EndDate = endDate.GetValueOrDefault(DateTime.UtcNow),
                        TotalCost = 0,
                        TimeFrame = "custom",
                        TopModelsBySpend = new List<ConduitLLM.Configuration.DTOs.Costs.DetailedCostDataDto>(),
                        TopProvidersBySpend = new List<ConduitLLM.Configuration.DTOs.Costs.DetailedCostDataDto>(),
                        TopVirtualKeysBySpend = new List<ConduitLLM.Configuration.DTOs.Costs.DetailedCostDataDto>()
                    };
                }

                return configDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cost dashboard data from Admin API");
                throw;
            }
        }
        
        /// <inheritdoc />
        public async Task<List<ConduitLLM.Configuration.DTOs.VirtualKey.VirtualKeyDto>> GetVirtualKeysAsync()
        {
            try
            {
                // Get virtual keys from Admin API
                var configVirtualKeyDtos = await _adminApiClient.GetAllVirtualKeysAsync();

                // Return the Configuration DTOs directly - we now use the same DTO type
                return configVirtualKeyDtos.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting virtual keys from Admin API");
                throw;
            }
        }
        
        /// <inheritdoc />
        public async Task<List<string>> GetAvailableModelsAsync()
        {
            try
            {
                // The Admin API client needs to provide a similar method to fetch available models
                // For now, we'll use the model provider mappings to get model names
                var mappings = await _adminApiClient.GetAllModelProviderMappingsAsync();
                return mappings.Select(m => m.ModelId).Distinct().ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available models from Admin API");
                throw;
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
                // The Admin API client needs to provide a method to fetch detailed cost data
                var configDtos = await _adminApiClient.GetDetailedCostDataAsync(startDate, endDate, virtualKeyId, modelName);
                if (configDtos == null)
                {
                    return new List<ConduitLLM.Configuration.DTOs.Costs.DetailedCostDataDto>();
                }

                // Convert to the proper type
                return configDtos.Select(dto => new ConduitLLM.Configuration.DTOs.Costs.DetailedCostDataDto
                {
                    Name = dto.Name ?? "Unknown",
                    Cost = dto.Cost,
                    Percentage = dto.Percentage
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting detailed cost data from Admin API");
                throw;
            }
        }
    }
}