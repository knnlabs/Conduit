using ConduitLLM.Configuration.Entities;
using ConfigDTO = ConduitLLM.Configuration.DTOs;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Services;

/// <summary>
/// Adapter service for cost dashboard data that can use either direct repository access or the Admin API
/// </summary>
public class CostDashboardServiceAdapter : ICostDashboardService
{
    private readonly CostDashboardService _repositoryService;
    private readonly IAdminApiClient _adminApiClient;
    private readonly AdminApiOptions _adminApiOptions;
    private readonly ILogger<CostDashboardServiceAdapter> _logger;
    
    /// <summary>
    /// Initializes a new instance of the CostDashboardServiceAdapter class
    /// </summary>
    /// <param name="repositoryService">The repository-based cost dashboard service</param>
    /// <param name="adminApiClient">The Admin API client</param>
    /// <param name="adminApiOptions">The Admin API options</param>
    /// <param name="logger">The logger</param>
    public CostDashboardServiceAdapter(
        CostDashboardService repositoryService,
        IAdminApiClient adminApiClient,
        IOptions<AdminApiOptions> adminApiOptions,
        ILogger<CostDashboardServiceAdapter> logger)
    {
        _repositoryService = repositoryService ?? throw new ArgumentNullException(nameof(repositoryService));
        _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
        _adminApiOptions = adminApiOptions?.Value ?? throw new ArgumentNullException(nameof(adminApiOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <inheritdoc />
    public async Task<ConfigDTO.CostDashboardDto> GetDashboardDataAsync(
        DateTime? startDate,
        DateTime? endDate,
        int? virtualKeyId = null,
        string? modelName = null)
    {
        if (_adminApiOptions.Enabled)
        {
            try
            {
                // The Admin API client needs to provide a similar method to fetch cost dashboard data
                var configDto = await _adminApiClient.GetCostDashboardAsync(startDate, endDate, virtualKeyId, modelName);

                if (configDto == null)
                {
                    return new ConfigDTO.CostDashboardDto
                    {
                        StartDate = startDate.GetValueOrDefault(DateTime.UtcNow.AddDays(-7)),
                        EndDate = endDate.GetValueOrDefault(DateTime.UtcNow),
                        TotalCost = 0,
                        TotalRequests = 0,
                        TotalInputTokens = 0,
                        TotalOutputTokens = 0,
                        CostTrends = new List<ConfigDTO.CostTrendDataDto>(),
                        CostByModel = new List<ConfigDTO.ModelCostDataDto>(),
                        CostByVirtualKey = new List<ConfigDTO.VirtualKeyCostDataDto>()
                    };
                }

                return configDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cost dashboard data from Admin API, falling back to repository");
                return await _repositoryService.GetDashboardDataAsync(startDate, endDate, virtualKeyId, modelName);
            }
        }
        
        return await _repositoryService.GetDashboardDataAsync(startDate, endDate, virtualKeyId, modelName);
    }
    
    /// <inheritdoc />
    public async Task<List<ConfigDTO.VirtualKey.VirtualKeyDto>> GetVirtualKeysAsync()
    {
        if (_adminApiOptions.Enabled)
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
                _logger.LogError(ex, "Error getting virtual keys from Admin API, falling back to repository");
                return await _repositoryService.GetVirtualKeysAsync();
            }
        }

        return await _repositoryService.GetVirtualKeysAsync();
    }
    
    /// <inheritdoc />
    public async Task<List<string>> GetAvailableModelsAsync()
    {
        if (_adminApiOptions.Enabled)
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
                _logger.LogError(ex, "Error getting available models from Admin API, falling back to repository");
                return await _repositoryService.GetAvailableModelsAsync();
            }
        }
        
        return await _repositoryService.GetAvailableModelsAsync();
    }
    
    /// <inheritdoc />
    public async Task<List<ConfigDTO.DetailedCostDataDto>> GetDetailedCostDataAsync(
        DateTime? startDate,
        DateTime? endDate,
        int? virtualKeyId = null,
        string? modelName = null)
    {
        if (_adminApiOptions.Enabled)
        {
            try
            {
                // The Admin API client needs to provide a method to fetch detailed cost data
                var configDtos = await _adminApiClient.GetDetailedCostDataAsync(startDate, endDate, virtualKeyId, modelName);
                if (configDtos == null)
                {
                    return new List<ConfigDTO.DetailedCostDataDto>();
                }

                // Return the Configuration DTOs directly - we now use the same DTO type
                return configDtos.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting detailed cost data from Admin API, falling back to repository");
                return await _repositoryService.GetDetailedCostDataAsync(startDate, endDate, virtualKeyId, modelName);
            }
        }
        
        return await _repositoryService.GetDetailedCostDataAsync(startDate, endDate, virtualKeyId, modelName);
    }
}