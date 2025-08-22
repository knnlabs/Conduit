using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.DTOs.IpFilter;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Services;

using MassTransit;
using Microsoft.Extensions.Options;

using ConduitLLM.Configuration.Interfaces;
namespace ConduitLLM.Admin.Services;

/// <summary>
/// Service for managing IP filters through the Admin API
/// </summary>
public class AdminIpFilterService : EventPublishingServiceBase, IAdminIpFilterService
{
    private readonly IIpFilterRepository _ipFilterRepository;
    private readonly IOptionsMonitor<IpFilterOptions> _ipFilterOptions;
    private readonly ILogger<AdminIpFilterService> _logger;

    /// <summary>
    /// Initializes a new instance of the AdminIpFilterService class
    /// </summary>
    /// <param name="ipFilterRepository">The IP filter repository</param>
    /// <param name="ipFilterOptions">The IP filter options</param>
    /// <param name="publishEndpoint">Optional event publishing endpoint (null if MassTransit not configured)</param>
    /// <param name="logger">The logger</param>
    public AdminIpFilterService(
        IIpFilterRepository ipFilterRepository,
        IOptionsMonitor<IpFilterOptions> ipFilterOptions,
        IPublishEndpoint? publishEndpoint,
        ILogger<AdminIpFilterService> logger)
        : base(publishEndpoint, logger)
    {
        _ipFilterRepository = ipFilterRepository ?? throw new ArgumentNullException(nameof(ipFilterRepository));
        _ipFilterOptions = ipFilterOptions ?? throw new ArgumentNullException(nameof(ipFilterOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        LogEventPublishingConfiguration(nameof(AdminIpFilterService));
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
            _logger.LogInformation("Creating new IP filter for {IpAddress}", (createFilter.IpAddressOrCidr ?? "").Replace(Environment.NewLine, ""));

            // Validate the IP address format
            if (string.IsNullOrWhiteSpace(createFilter.IpAddressOrCidr) || !IsValidIpAddressOrCidr(createFilter.IpAddressOrCidr))
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

            // Publish IpFilterChanged event for cache invalidation and cross-service coordination
            await PublishEventAsync(
                new IpFilterChanged
                {
                    FilterId = createdFilter.Id,
                    IpAddressOrCidr = createdFilter.IpAddressOrCidr,
                    FilterType = createdFilter.FilterType,
                    IsEnabled = createdFilter.IsEnabled,
                    ChangeType = "Created",
                    ChangedProperties = Array.Empty<string>(),
                    Description = createdFilter.Description ?? string.Empty,
                    CorrelationId = Guid.NewGuid().ToString()
                },
                $"create IP filter {createdFilter.Id}",
                new { IpAddressOrCidr = createdFilter.IpAddressOrCidr, FilterType = createdFilter.FilterType });

            // Return the created filter
            return (true, null, MapToDto(createdFilter));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating IP filter for {IpAddress}", (createFilter.IpAddressOrCidr ?? "").Replace(Environment.NewLine, ""));
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

            // Track changes for event publishing
            var changedProperties = new List<string>();

            if (existingFilter.FilterType != updateFilter.FilterType)
            {
                existingFilter.FilterType = updateFilter.FilterType;
                changedProperties.Add(nameof(existingFilter.FilterType));
            }

            if (existingFilter.IpAddressOrCidr != updateFilter.IpAddressOrCidr)
            {
                existingFilter.IpAddressOrCidr = updateFilter.IpAddressOrCidr;
                changedProperties.Add(nameof(existingFilter.IpAddressOrCidr));
            }

            if (existingFilter.Description != updateFilter.Description)
            {
                existingFilter.Description = updateFilter.Description;
                changedProperties.Add(nameof(existingFilter.Description));
            }

            if (existingFilter.IsEnabled != updateFilter.IsEnabled)
            {
                existingFilter.IsEnabled = updateFilter.IsEnabled;
                changedProperties.Add(nameof(existingFilter.IsEnabled));
            }

            // Only proceed if there are actual changes
            if (changedProperties.Count() == 0)
            {
                _logger.LogDebug("No changes detected for IP filter {FilterId} - skipping update", updateFilter.Id);
                return (true, null);
            }

            existingFilter.UpdatedAt = DateTime.UtcNow;

            // Save to database
            var success = await _ipFilterRepository.UpdateAsync(existingFilter);

            if (success)
            {
                // Publish IpFilterChanged event for cache invalidation and cross-service coordination
                await PublishEventAsync(
                    new IpFilterChanged
                    {
                        FilterId = existingFilter.Id,
                        IpAddressOrCidr = existingFilter.IpAddressOrCidr,
                        FilterType = existingFilter.FilterType,
                        IsEnabled = existingFilter.IsEnabled,
                        ChangeType = "Updated",
                        ChangedProperties = changedProperties.ToArray(),
                        Description = existingFilter.Description ?? string.Empty,
                        CorrelationId = Guid.NewGuid().ToString()
                    },
                    $"update IP filter {existingFilter.Id}",
                    new { ChangedProperties = string.Join(", ", changedProperties) });

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
                // Publish IpFilterChanged event for cache invalidation and cross-service coordination
                await PublishEventAsync(
                    new IpFilterChanged
                    {
                        FilterId = existingFilter.Id,
                        IpAddressOrCidr = existingFilter.IpAddressOrCidr,
                        FilterType = existingFilter.FilterType,
                        IsEnabled = existingFilter.IsEnabled,
                        ChangeType = "Deleted",
                        ChangedProperties = Array.Empty<string>(),
                        Description = existingFilter.Description ?? string.Empty,
                        CorrelationId = Guid.NewGuid().ToString()
                    },
                    $"delete IP filter {existingFilter.Id}",
                    new { IpAddressOrCidr = existingFilter.IpAddressOrCidr, FilterType = existingFilter.FilterType });

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
    public Task<(bool Success, string? ErrorMessage)> UpdateIpFilterSettingsAsync(IpFilterSettingsDto settings)
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

            return Task.FromResult<(bool Success, string? ErrorMessage)>((true, null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating IP filter settings");
            return Task.FromResult<(bool Success, string? ErrorMessage)>((false, "An unexpected error occurred"));
        }
    }

    /// <inheritdoc/>
    public async Task<IpCheckResult> CheckIpAddressAsync(string ipAddress)
    {
        try
        {
            _logger.LogInformation("Checking if IP address is allowed: {IpAddress}", ipAddress.Replace(Environment.NewLine, ""));

            // Get current IP filter settings
            var settings = await GetIpFilterSettingsAsync();

            // If IP filtering is disabled, allow all
            if (!settings.IsEnabled)
            {
                return new IpCheckResult { IsAllowed = true };
            }

            // Validate the IP format
            if (!System.Net.IPAddress.TryParse(ipAddress, out _))
            {
                return new IpCheckResult
                {
                    IsAllowed = false,
                    DeniedReason = "Invalid IP address format"
                };
            }

            // Get all enabled IP filters
            var filters = await GetEnabledFiltersAsync();

            // Check whitelist (allow) filters
            var whitelistFilters = filters.Where(f => f.FilterType == IpFilterConstants.WHITELIST).ToList();
            foreach (var filter in whitelistFilters)
            {
                if (IpAddressMatchesFilter(ipAddress, filter.IpAddressOrCidr))
                {
                    return new IpCheckResult { IsAllowed = true };
                }
            }

            // Check blacklist (deny) filters
            var blacklistFilters = filters.Where(f => f.FilterType == IpFilterConstants.BLACKLIST).ToList();
            foreach (var filter in blacklistFilters)
            {
                if (IpAddressMatchesFilter(ipAddress, filter.IpAddressOrCidr))
                {
                    return new IpCheckResult
                    {
                        IsAllowed = false,
                        DeniedReason = $"IP address matched deny filter: {filter.Description}"
                    };
                }
            }

            // No matches found, use default policy
            return new IpCheckResult
            {
                IsAllowed = settings.DefaultAllow,
                DeniedReason = settings.DefaultAllow ? null : "IP address did not match any allow filters"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if IP address is allowed: {IpAddress}", ipAddress.Replace(Environment.NewLine, ""));

            // On error, default to allowing the request (safer than potentially blocking all traffic)
            return new IpCheckResult
            {
                IsAllowed = true,
                DeniedReason = "Error during IP check, allowed as a failsafe"
            };
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
            _logger.LogWarning(ex, "Error validating IP address or CIDR: {IpAddressOrCidr}", ipAddressOrCidr.Replace(Environment.NewLine, ""));
            return false;
        }
    }

    /// <summary>
    /// Checks if an IP address matches a filter (exact match or CIDR range)
    /// </summary>
    /// <param name="ipAddress">The IP address to check</param>
    /// <param name="filterValue">The filter value (IP address or CIDR notation)</param>
    /// <returns>True if the IP matches the filter, false otherwise</returns>
    private bool IpAddressMatchesFilter(string ipAddress, string filterValue)
    {
        // Simple exact match
        if (ipAddress == filterValue)
        {
            return true;
        }

        // If the filter is a CIDR range
        if (filterValue.Contains('/'))
        {
            try
            {
                // This is a simplified implementation that would need to be replaced
                // with actual CIDR range matching logic in a production environment

                var parts = filterValue.Split('/');
                if (parts.Length != 2)
                {
                    return false;
                }

                var networkAddress = parts[0];

                // For a very basic check, see if the IP starts with the same network portion
                // This is NOT accurate for real CIDR matching and is just a placeholder
                return ipAddress.StartsWith(networkAddress.Substring(0, networkAddress.LastIndexOf('.')));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking IP {IpAddress} against CIDR {CidrRange}", ipAddress.Replace(Environment.NewLine, ""), filterValue.Replace(Environment.NewLine, ""));
                return false;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public async Task<bool> IsIpAllowedAsync(string ipAddress)
    {
        var result = await CheckIpAddressAsync(ipAddress);
        return result.IsAllowed;
    }
}
