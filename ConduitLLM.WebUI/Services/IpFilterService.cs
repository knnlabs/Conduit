using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.DTOs.IpFilter;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.WebUI.Services;

/// <summary>
/// Service for managing IP filters
/// </summary>
public class IpFilterService : IIpFilterService
{
    private readonly IIpFilterRepository _repository;
    private readonly IGlobalSettingService _globalSettingService;
    private readonly IpFilterValidator _validator;
    private readonly IpFilterMatcher _matcher;
    private readonly IOptions<IpFilterOptions> _options;
    private readonly ILogger<IpFilterService> _logger;
    
    private const string SettingsEnabledKey = "IpFilter_Enabled";
    private const string SettingsDefaultAllowKey = "IpFilter_DefaultAllow";
    private const string SettingsBypassAdminUiKey = "IpFilter_BypassAdminUi";
    private const string SettingsExcludedEndpointsKey = "IpFilter_ExcludedEndpoints";
    
    /// <summary>
    /// Initializes a new instance of the <see cref="IpFilterService"/> class
    /// </summary>
    public IpFilterService(
        IIpFilterRepository repository,
        IGlobalSettingService globalSettingService,
        ILogger<IpFilterService> logger,
        ILogger<IpFilterValidator> validatorLogger,
        ILogger<IpFilterMatcher> matcherLogger,
        IOptions<IpFilterOptions> options)
    {
        _repository = repository;
        _globalSettingService = globalSettingService;
        _logger = logger;
        _validator = new IpFilterValidator(validatorLogger);
        _matcher = new IpFilterMatcher(matcherLogger);
        _options = options;
    }
    
    /// <inheritdoc/>
    public async Task<IEnumerable<IpFilterDto>> GetAllFiltersAsync()
    {
        var filters = await _repository.GetAllAsync();
        return filters.Select(MapEntityToDto);
    }
    
    /// <inheritdoc/>
    public async Task<IEnumerable<IpFilterDto>> GetEnabledFiltersAsync()
    {
        var filters = await _repository.GetEnabledAsync();
        return filters.Select(MapEntityToDto);
    }
    
    /// <inheritdoc/>
    public async Task<IpFilterDto?> GetFilterByIdAsync(int id)
    {
        var filter = await _repository.GetByIdAsync(id);
        return filter != null ? MapEntityToDto(filter) : null;
    }
    
    /// <inheritdoc/>
    public async Task<(bool Success, string? ErrorMessage, IpFilterDto? Filter)> CreateFilterAsync(CreateIpFilterDto filter)
    {
        try
        {
            // Standardize the IP address or CIDR
            filter.IpAddressOrCidr = IpAddressValidator.StandardizeIpAddressOrCidr(filter.IpAddressOrCidr) ?? filter.IpAddressOrCidr;
            
            // Get all existing filters for validation
            var existingFilters = await _repository.GetAllAsync();
            
            // Validate the filter
            var (isValid, errorMessage) = _validator.ValidateFilter(filter, existingFilters);
            if (!isValid)
            {
                return (false, errorMessage, null);
            }
            
            // Create the entity
            var entity = new IpFilterEntity
            {
                FilterType = filter.FilterType,
                IpAddressOrCidr = filter.IpAddressOrCidr,
                Description = filter.Description ?? string.Empty,
                IsEnabled = filter.IsEnabled,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            // Save to database
            var result = await _repository.AddAsync(entity);
            
            return (true, null, MapEntityToDto(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating IP filter for {IpAddressOrCidr}", filter.IpAddressOrCidr);
            return (false, $"An error occurred: {ex.Message}", null);
        }
    }
    
    /// <inheritdoc/>
    public async Task<(bool Success, string? ErrorMessage)> UpdateFilterAsync(UpdateIpFilterDto filter)
    {
        try
        {
            // Standardize the IP address or CIDR
            filter.IpAddressOrCidr = IpAddressValidator.StandardizeIpAddressOrCidr(filter.IpAddressOrCidr) ?? filter.IpAddressOrCidr;
            
            // Get all existing filters for validation
            var existingFilters = await _repository.GetAllAsync();
            
            // Check if filter exists
            var existingFilter = await _repository.GetByIdAsync(filter.Id);
            if (existingFilter == null)
            {
                return (false, $"IP filter with ID {filter.Id} not found.");
            }
            
            // Validate the filter
            // We need to explicitly cast here because we're passing an UpdateIpFilterDto to a method that expects CreateIpFilterDto
            var createDto = new CreateIpFilterDto
            {
                FilterType = filter.FilterType,
                IpAddressOrCidr = filter.IpAddressOrCidr,
                Description = filter.Description ?? string.Empty,
                IsEnabled = filter.IsEnabled
            };
            var (isValid, errorMessage) = _validator.ValidateFilter(createDto, existingFilters, true);
            if (!isValid)
            {
                return (false, errorMessage);
            }
            
            // Update entity
            existingFilter.FilterType = filter.FilterType;
            existingFilter.IpAddressOrCidr = filter.IpAddressOrCidr;
            existingFilter.Description = filter.Description ?? string.Empty;
            existingFilter.IsEnabled = filter.IsEnabled;
            existingFilter.UpdatedAt = DateTime.UtcNow;
            
            // Save to database
            var success = await _repository.UpdateAsync(existingFilter);
            
            return success 
                ? (true, null) 
                : (false, "Failed to update IP filter in the database.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating IP filter with ID {Id}", filter.Id);
            return (false, $"An error occurred: {ex.Message}");
        }
    }
    
    /// <inheritdoc/>
    public async Task<(bool Success, string? ErrorMessage)> DeleteFilterAsync(int id)
    {
        try
        {
            var success = await _repository.DeleteAsync(id);
            
            return success 
                ? (true, null) 
                : (false, $"IP filter with ID {id} not found.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting IP filter with ID {Id}", id);
            return (false, $"An error occurred: {ex.Message}");
        }
    }
    
    /// <inheritdoc/>
    public async Task<bool> IsIpAllowedAsync(string ipAddress)
    {
        try
        {
            // Get settings
            var settings = await GetIpFilterSettingsAsync();
            
            // If filtering is disabled, allow all IPs
            if (!settings.IsEnabled)
            {
                return true;
            }
            
            // Get all enabled filters
            var filters = await _repository.GetEnabledAsync();
            
            // Check if IP is allowed
            return _matcher.IsIpAllowed(ipAddress, filters, settings.DefaultAllow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if IP {IpAddress} is allowed", ipAddress);
            // Default to allowing if there's an error
            return true;
        }
    }
    
    /// <inheritdoc/>
    public async Task<IpFilterSettings> GetIpFilterSettingsAsync()
    {
        try
        {
            var settings = new IpFilterSettings
            {
                // Default values from options
                IsEnabled = _options.Value.Enabled,
                DefaultAllow = _options.Value.DefaultAllow,
                BypassForAdminUi = _options.Value.BypassForAdminUi,
                ExcludedEndpoints = new List<string>(_options.Value.ExcludedEndpoints),
                FilterMode = _options.Value.DefaultAllow ? "permissive" : "restrictive"
            };

            // Override from environment variables
            var envEnabled = Environment.GetEnvironmentVariable(IpFilterConstants.IP_FILTERING_ENABLED_ENV);
            if (!string.IsNullOrEmpty(envEnabled))
            {
                settings.IsEnabled = bool.TryParse(envEnabled, out var enabled) && enabled;
            }

            // Override from database settings
            var dbEnabled = await _globalSettingService.GetSettingAsync(SettingsEnabledKey);
            if (!string.IsNullOrEmpty(dbEnabled))
            {
                settings.IsEnabled = bool.TryParse(dbEnabled, out var enabled) && enabled;
            }

            // Get other settings from database
            var dbDefaultAllow = await _globalSettingService.GetSettingAsync(SettingsDefaultAllowKey);
            if (!string.IsNullOrEmpty(dbDefaultAllow))
            {
                settings.DefaultAllow = bool.TryParse(dbDefaultAllow, out var defaultAllow) && defaultAllow;
                // Update filter mode based on default allow setting
                settings.FilterMode = settings.DefaultAllow ? "permissive" : "restrictive";
            }

            var dbBypassAdminUi = await _globalSettingService.GetSettingAsync(SettingsBypassAdminUiKey);
            if (!string.IsNullOrEmpty(dbBypassAdminUi))
            {
                settings.BypassForAdminUi = bool.TryParse(dbBypassAdminUi, out var bypassAdminUi) && bypassAdminUi;
            }

            var dbExcludedEndpoints = await _globalSettingService.GetSettingAsync(SettingsExcludedEndpointsKey);
            if (!string.IsNullOrEmpty(dbExcludedEndpoints))
            {
                settings.ExcludedEndpoints = dbExcludedEndpoints.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(e => e.Trim())
                    .Where(e => !string.IsNullOrEmpty(e))
                    .ToList();
            }

            // Get whitelist and blacklist filters
            var filters = await _repository.GetEnabledAsync();
            settings.WhitelistFilters = filters
                .Where(f => f.FilterType == "whitelist")
                .Select(MapEntityToDto)
                .ToList();

            settings.BlacklistFilters = filters
                .Where(f => f.FilterType == "blacklist")
                .Select(MapEntityToDto)
                .ToList();

            return settings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting IP filter settings");
            // Return default settings on error
            return new IpFilterSettings
            {
                IsEnabled = _options.Value.Enabled,
                DefaultAllow = _options.Value.DefaultAllow,
                BypassForAdminUi = _options.Value.BypassForAdminUi,
                ExcludedEndpoints = new List<string>(_options.Value.ExcludedEndpoints),
                FilterMode = _options.Value.DefaultAllow ? "permissive" : "restrictive"
            };
        }
    }
    
    /// <inheritdoc/>
    public async Task<(bool Success, string? ErrorMessage)> UpdateIpFilterSettingsAsync(IpFilterSettings settings)
    {
        try
        {
            // Save settings to database
            await _globalSettingService.SetSettingAsync(
                SettingsEnabledKey,
                settings.IsEnabled.ToString().ToLowerInvariant());

            await _globalSettingService.SetSettingAsync(
                SettingsDefaultAllowKey,
                settings.DefaultAllow.ToString().ToLowerInvariant());

            await _globalSettingService.SetSettingAsync(
                SettingsBypassAdminUiKey,
                settings.BypassForAdminUi.ToString().ToLowerInvariant());

            var excludedEndpoints = string.Join(",", settings.ExcludedEndpoints.Where(e => !string.IsNullOrWhiteSpace(e)));
            await _globalSettingService.SetSettingAsync(
                SettingsExcludedEndpointsKey,
                excludedEndpoints);
                
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating IP filter settings");
            return (false, $"An error occurred: {ex.Message}");
        }
    }
    
    private static IpFilterDto MapEntityToDto(IpFilterEntity entity)
    {
        return new IpFilterDto
        {
            Id = entity.Id,
            FilterType = entity.FilterType,
            IpAddressOrCidr = entity.IpAddressOrCidr,
            Description = entity.Description,
            IsEnabled = entity.IsEnabled,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}