using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.DTOs.IpFilter;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Configuration.Repositories;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Admin.Services;

/// <summary>
/// Service for managing IP filters through the Admin API
/// </summary>
public class AdminIpFilterService : IAdminIpFilterService
{
    private readonly IIpFilterRepository _ipFilterRepository;
    private readonly IOptionsMonitor<IpFilterOptions> _ipFilterOptions;
    private readonly ILogger<AdminIpFilterService> _logger;
    
    /// <summary>
    /// Initializes a new instance of the AdminIpFilterService class
    /// </summary>
    /// <param name="ipFilterRepository">The IP filter repository</param>
    /// <param name="ipFilterOptions">The IP filter options</param>
    /// <param name="logger">The logger</param>
    public AdminIpFilterService(
        IIpFilterRepository ipFilterRepository,
        IOptionsMonitor<IpFilterOptions> ipFilterOptions,
        ILogger<AdminIpFilterService> logger)
    {
        _ipFilterRepository = ipFilterRepository ?? throw new ArgumentNullException(nameof(ipFilterRepository));
        _ipFilterOptions = ipFilterOptions ?? throw new ArgumentNullException(nameof(ipFilterOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <inheritdoc/>
    public async Task<IEnumerable<IpFilterDto>> GetAllFiltersAsync()
    {
        try
        {
            _logger.LogInformation("Getting all IP filters");
            
            var filters = await _ipFilterRepository.GetAllAsync();
            return filters.Select(MapToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all IP filters");
            return Enumerable.Empty<IpFilterDto>();
        }
    }
    
    /// <inheritdoc/>
    public async Task<IEnumerable<IpFilterDto>> GetEnabledFiltersAsync()
    {
        try
        {
            _logger.LogInformation("Getting enabled IP filters");
            
            var filters = await _ipFilterRepository.GetEnabledAsync();
            return filters.Select(MapToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting enabled IP filters");
            return Enumerable.Empty<IpFilterDto>();
        }
    }
    
    /// <inheritdoc/>
    public async Task<IpFilterDto?> GetFilterByIdAsync(int id)
    {
        try
        {
            _logger.LogInformation("Getting IP filter with ID: {FilterId}", id);
            
            var filter = await _ipFilterRepository.GetByIdAsync(id);
            return filter != null ? MapToDto(filter) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting IP filter with ID {FilterId}", id);
            return null;
        }
    }
    
    /// <inheritdoc/>
    public async Task<(bool Success, string? ErrorMessage, IpFilterDto? Filter)> CreateFilterAsync(CreateIpFilterDto createFilter)
    {
        try
        {
            _logger.LogInformation("Creating new IP filter for {IpAddress}", createFilter.IpAddressOrCidr);
            
            // Validate the IP address format
            if (!IsValidIpAddressOrCidr(createFilter.IpAddressOrCidr))
            {
                return (false, "Invalid IP address or CIDR format", null);
            }
            
            // Map to entity
            var entity = new IpFilterEntity
            {
                FilterType = createFilter.FilterType,
                IpAddressOrCidr = createFilter.IpAddressOrCidr,
                Description = createFilter.Description,
                IsEnabled = createFilter.IsEnabled,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            // Save to database
            var createdFilter = await _ipFilterRepository.AddAsync(entity);
            
            // Return the created filter
            return (true, null, MapToDto(createdFilter));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating IP filter for {IpAddress}", createFilter.IpAddressOrCidr);
            return (false, "An unexpected error occurred", null);
        }
    }
    
    /// <inheritdoc/>
    public async Task<(bool Success, string? ErrorMessage)> UpdateFilterAsync(UpdateIpFilterDto updateFilter)
    {
        try
        {
            _logger.LogInformation("Updating IP filter with ID: {FilterId}", updateFilter.Id);
            
            // Check if the filter exists
            var existingFilter = await _ipFilterRepository.GetByIdAsync(updateFilter.Id);
            if (existingFilter == null)
            {
                return (false, $"IP filter with ID {updateFilter.Id} not found");
            }
            
            // Validate the IP address format
            if (!IsValidIpAddressOrCidr(updateFilter.IpAddressOrCidr))
            {
                return (false, "Invalid IP address or CIDR format");
            }
            
            // Update the entity
            existingFilter.FilterType = updateFilter.FilterType;
            existingFilter.IpAddressOrCidr = updateFilter.IpAddressOrCidr;
            existingFilter.Description = updateFilter.Description;
            existingFilter.IsEnabled = updateFilter.IsEnabled;
            existingFilter.UpdatedAt = DateTime.UtcNow;
            
            // Save to database
            var success = await _ipFilterRepository.UpdateAsync(existingFilter);
            
            if (success)
            {
                return (true, null);
            }
            else
            {
                return (false, "Failed to update the IP filter");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating IP filter with ID {FilterId}", updateFilter.Id);
            return (false, "An unexpected error occurred");
        }
    }
    
    /// <inheritdoc/>
    public async Task<(bool Success, string? ErrorMessage)> DeleteFilterAsync(int id)
    {
        try
        {
            _logger.LogInformation("Deleting IP filter with ID: {FilterId}", id);
            
            // Check if the filter exists
            var existingFilter = await _ipFilterRepository.GetByIdAsync(id);
            if (existingFilter == null)
            {
                return (false, $"IP filter with ID {id} not found");
            }
            
            // Delete from database
            var success = await _ipFilterRepository.DeleteAsync(id);
            
            if (success)
            {
                return (true, null);
            }
            else
            {
                return (false, "Failed to delete the IP filter");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting IP filter with ID {FilterId}", id);
            return (false, "An unexpected error occurred");
        }
    }
    
    /// <inheritdoc/>
    public Task<IpFilterSettingsDto> GetIpFilterSettingsAsync()
    {
        try
        {
            _logger.LogInformation("Getting IP filter settings");
            
            var options = _ipFilterOptions.CurrentValue;
            
            var settings = new IpFilterSettingsDto
            {
                IsEnabled = options.Enabled,
                DefaultAllow = options.DefaultAllow,
                BypassForAdminUi = options.BypassForAdminUi,
                ExcludedEndpoints = options.ExcludedEndpoints.ToList()
            };
            
            return Task.FromResult(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting IP filter settings");
            
            // Return default settings on error
            return Task.FromResult(new IpFilterSettingsDto
            {
                IsEnabled = false,
                DefaultAllow = true,
                BypassForAdminUi = true,
                ExcludedEndpoints = new List<string> { "/api/v1/health" }
            });
        }
    }
    
    /// <inheritdoc/>
    public async Task<(bool Success, string? ErrorMessage)> UpdateIpFilterSettingsAsync(IpFilterSettingsDto settings)
    {
        try
        {
            _logger.LogInformation("Updating IP filter settings: Enabled={Enabled}, DefaultAllow={DefaultAllow}", 
                settings.IsEnabled, settings.DefaultAllow);
            
            // Validate settings
            if (settings.ExcludedEndpoints == null)
            {
                settings.ExcludedEndpoints = new List<string>();
            }
            
            // In a real implementation, we would update the appsettings.json file or a database settings table
            // For now, we'll just log the settings and return success
            
            // Example code for updating a database-stored setting:
            // await _settingsRepository.UpdateSettingAsync("IpFilter:Enabled", settings.IsEnabled.ToString());
            // await _settingsRepository.UpdateSettingAsync("IpFilter:DefaultAllow", settings.DefaultAllow.ToString());
            // await _settingsRepository.UpdateSettingAsync("IpFilter:BypassForAdminUi", settings.BypassForAdminUi.ToString());
            // await _settingsRepository.UpdateSettingAsync("IpFilter:ExcludedEndpoints", JsonSerializer.Serialize(settings.ExcludedEndpoints));
            
            _logger.LogWarning("IP filter settings updated in memory only - actual settings update implementation needed");
            
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating IP filter settings");
            return (false, "An unexpected error occurred");
        }
    }
    
    /// <summary>
    /// Maps an IP filter entity to a DTO
    /// </summary>
    /// <param name="entity">The entity to map</param>
    /// <returns>The mapped DTO</returns>
    private static IpFilterDto MapToDto(IpFilterEntity entity)
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
    
    /// <summary>
    /// Validates if a string is a valid IP address or CIDR notation
    /// </summary>
    /// <param name="ipAddressOrCidr">The string to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    private bool IsValidIpAddressOrCidr(string ipAddressOrCidr)
    {
        if (string.IsNullOrWhiteSpace(ipAddressOrCidr))
        {
            return false;
        }
        
        try
        {
            // Check if it's a CIDR notation (e.g., 192.168.1.0/24)
            if (ipAddressOrCidr.Contains('/'))
            {
                var parts = ipAddressOrCidr.Split('/');
                if (parts.Length != 2)
                {
                    return false;
                }
                
                // Validate IP part
                if (!System.Net.IPAddress.TryParse(parts[0], out _))
                {
                    return false;
                }
                
                // Validate prefix length
                if (!int.TryParse(parts[1], out int prefixLength))
                {
                    return false;
                }
                
                // For IPv4, prefix length should be between 0 and 32
                // For IPv6, prefix length should be between 0 and 128
                // We'll accept 0-128 for simplicity
                return prefixLength >= 0 && prefixLength <= 128;
            }
            else
            {
                // It's a simple IP address
                return System.Net.IPAddress.TryParse(ipAddressOrCidr, out _);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error validating IP address or CIDR: {IpAddressOrCidr}", ipAddressOrCidr);
            return false;
        }
    }
}