using ConduitLLM.Configuration.DTOs.IpFilter;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Models;
using ConduitLLM.WebUI.Options;
using Microsoft.Extensions.Options;

namespace ConduitLLM.WebUI.Services;

/// <summary>
/// Adapter service for IP filtering that can use either direct repository access or the Admin API
/// </summary>
public class IpFilterServiceAdapter : IIpFilterService
{
    private readonly IIpFilterService _repositoryService;
    private readonly IAdminApiClient _adminApiClient;
    private readonly AdminApiOptions _adminApiOptions;
    private readonly ILogger<IpFilterServiceAdapter> _logger;
    
    /// <summary>
    /// Initializes a new instance of the IpFilterServiceAdapter class
    /// </summary>
    /// <param name="repositoryService">The repository-based IP filter service</param>
    /// <param name="adminApiClient">The Admin API client</param>
    /// <param name="adminApiOptions">The Admin API options</param>
    /// <param name="logger">The logger</param>
    public IpFilterServiceAdapter(
        IpFilterService repositoryService,
        IAdminApiClient adminApiClient,
        IOptions<AdminApiOptions> adminApiOptions,
        ILogger<IpFilterServiceAdapter> logger)
    {
        _repositoryService = repositoryService ?? throw new ArgumentNullException(nameof(repositoryService));
        _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
        _adminApiOptions = adminApiOptions?.Value ?? throw new ArgumentNullException(nameof(adminApiOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <inheritdoc />
    public async Task<IEnumerable<IpFilterDto>> GetAllFiltersAsync()
    {
        if (_adminApiOptions.Enabled)
        {
            try
            {
                return await _adminApiClient.GetAllIpFiltersAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all IP filters from Admin API, falling back to repository");
                return await _repositoryService.GetAllFiltersAsync();
            }
        }
        
        return await _repositoryService.GetAllFiltersAsync();
    }
    
    /// <inheritdoc />
    public async Task<IEnumerable<IpFilterDto>> GetEnabledFiltersAsync()
    {
        if (_adminApiOptions.Enabled)
        {
            try
            {
                return await _adminApiClient.GetEnabledIpFiltersAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting enabled IP filters from Admin API, falling back to repository");
                return await _repositoryService.GetEnabledFiltersAsync();
            }
        }
        
        return await _repositoryService.GetEnabledFiltersAsync();
    }
    
    /// <inheritdoc />
    public async Task<IpFilterDto?> GetFilterByIdAsync(int id)
    {
        if (_adminApiOptions.Enabled)
        {
            try
            {
                return await _adminApiClient.GetIpFilterByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting IP filter with ID {Id} from Admin API, falling back to repository", id);
                return await _repositoryService.GetFilterByIdAsync(id);
            }
        }
        
        return await _repositoryService.GetFilterByIdAsync(id);
    }
    
    /// <inheritdoc />
    public async Task<(bool Success, string? ErrorMessage, IpFilterDto? Filter)> CreateFilterAsync(CreateIpFilterDto filter)
    {
        if (_adminApiOptions.Enabled)
        {
            try
            {
                var result = await _adminApiClient.CreateIpFilterAsync(filter);
                return result != null 
                    ? (true, null, result) 
                    : (false, "Failed to create IP filter through Admin API", null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating IP filter through Admin API, falling back to repository");
                return await _repositoryService.CreateFilterAsync(filter);
            }
        }
        
        return await _repositoryService.CreateFilterAsync(filter);
    }
    
    /// <inheritdoc />
    public async Task<(bool Success, string? ErrorMessage)> UpdateFilterAsync(UpdateIpFilterDto filter)
    {
        if (_adminApiOptions.Enabled)
        {
            try
            {
                var updatedFilter = await _adminApiClient.UpdateIpFilterAsync(filter.Id, filter);
                return updatedFilter != null
                    ? (true, null)
                    : (false, "Failed to update IP filter through Admin API");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating IP filter through Admin API, falling back to repository");
                return await _repositoryService.UpdateFilterAsync(filter);
            }
        }
        
        return await _repositoryService.UpdateFilterAsync(filter);
    }
    
    /// <inheritdoc />
    public async Task<(bool Success, string? ErrorMessage)> DeleteFilterAsync(int id)
    {
        if (_adminApiOptions.Enabled)
        {
            try
            {
                var success = await _adminApiClient.DeleteIpFilterAsync(id);
                return success 
                    ? (true, null) 
                    : (false, "Failed to delete IP filter through Admin API");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting IP filter through Admin API, falling back to repository");
                return await _repositoryService.DeleteFilterAsync(id);
            }
        }
        
        return await _repositoryService.DeleteFilterAsync(id);
    }
    
    /// <inheritdoc />
    public async Task<bool> IsIpAllowedAsync(string ipAddress)
    {
        // Always use the repository service for this method since it's performance-critical
        // and doesn't involve administrative functions
        return await _repositoryService.IsIpAllowedAsync(ipAddress);
    }
    
    /// <inheritdoc />
    public async Task<IpFilterSettings> GetIpFilterSettingsAsync()
    {
        if (_adminApiOptions.Enabled)
        {
            try
            {
                var settings = await _adminApiClient.GetIpFilterSettingsAsync();
                
                // Convert from DTO to model
                return new IpFilterSettings
                {
                    IsEnabled = settings.IsEnabled,
                    DefaultAllow = settings.DefaultAllow,
                    BypassForAdminUi = settings.BypassForAdminUi,
                    ExcludedEndpoints = settings.ExcludedEndpoints,
                    FilterMode = settings.FilterMode,
                    WhitelistFilters = settings.WhitelistFilters,
                    BlacklistFilters = settings.BlacklistFilters
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting IP filter settings from Admin API, falling back to repository");
                return await _repositoryService.GetIpFilterSettingsAsync();
            }
        }
        
        return await _repositoryService.GetIpFilterSettingsAsync();
    }
    
    /// <inheritdoc />
    public async Task<(bool Success, string? ErrorMessage)> UpdateIpFilterSettingsAsync(IpFilterSettings settings)
    {
        if (_adminApiOptions.Enabled)
        {
            try
            {
                // Convert from model to DTO
                var settingsDto = new IpFilterSettingsDto
                {
                    IsEnabled = settings.IsEnabled,
                    DefaultAllow = settings.DefaultAllow,
                    BypassForAdminUi = settings.BypassForAdminUi,
                    ExcludedEndpoints = settings.ExcludedEndpoints,
                    FilterMode = settings.FilterMode,
                    WhitelistFilters = settings.WhitelistFilters,
                    BlacklistFilters = settings.BlacklistFilters
                };
                
                var success = await _adminApiClient.UpdateIpFilterSettingsAsync(settingsDto);
                return success 
                    ? (true, null) 
                    : (false, "Failed to update IP filter settings through Admin API");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating IP filter settings through Admin API, falling back to repository");
                return await _repositoryService.UpdateIpFilterSettingsAsync(settings);
            }
        }
        
        return await _repositoryService.UpdateIpFilterSettingsAsync(settings);
    }
}