using ConduitLLM.Admin.Extensions;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Utilities;
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
    private readonly IGlobalSettingRepository _globalSettingRepository;
    private readonly IOptionsMonitor<IpFilterOptions> _ipFilterOptions;
    private readonly ILogger<AdminIpFilterService> _logger;

    // Setting keys for IP filter configuration
    private const string SettingKeyEnabled = "IpFilter:Enabled";
    private const string SettingKeyDefaultAllow = "IpFilter:DefaultAllow";
    private const string SettingKeyBypassForAdminUi = "IpFilter:BypassForAdminUi";
    private const string SettingKeyExcludedEndpoints = "IpFilter:ExcludedEndpoints";

    /// <summary>
    /// Initializes a new instance of the AdminIpFilterService class
    /// </summary>
    /// <param name="ipFilterRepository">The IP filter repository</param>
    /// <param name="globalSettingRepository">The global settings repository for persisting IP filter settings</param>
    /// <param name="ipFilterOptions">The IP filter options</param>
    /// <param name="publishEndpoint">Optional event publishing endpoint (null if MassTransit not configured)</param>
    /// <param name="logger">The logger</param>
    public AdminIpFilterService(
        IIpFilterRepository ipFilterRepository,
        IGlobalSettingRepository globalSettingRepository,
        IOptionsMonitor<IpFilterOptions> ipFilterOptions,
        ILogger<AdminIpFilterService> logger,
        IPublishEndpoint? publishEndpoint = null)
        : base(publishEndpoint, logger)
    {
        _ipFilterRepository = ipFilterRepository ?? throw new ArgumentNullException(nameof(ipFilterRepository));
        _globalSettingRepository = globalSettingRepository ?? throw new ArgumentNullException(nameof(globalSettingRepository));
        _ipFilterOptions = ipFilterOptions ?? throw new ArgumentNullException(nameof(ipFilterOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        LogEventPublishingConfiguration(nameof(AdminIpFilterService));
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<IpFilterDto>> GetAllFiltersAsync()
    {
        try
        {
            _logger.LogDebug("Getting all IP filters");

            var filters = await _ipFilterRepository.GetAllUnboundedAsync();
            return filters.Select(f => f.ToDto());
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
            _logger.LogDebug("Getting enabled IP filters");

            var filters = await _ipFilterRepository.GetEnabledAsync();
            return filters.Select(f => f.ToDto());
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
            _logger.LogDebug("Getting IP filter with ID: {FilterId}", id);

            var filter = await _ipFilterRepository.GetByIdAsync(id);
            return filter?.ToDto();
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
            _logger.LogDebug("Creating new IP filter for {IpAddress}", (LoggingSanitizer.S(createFilter.IpAddressOrCidr ?? "")));

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
            return (true, null, createdFilter.ToDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating IP filter for {IpAddress}", (LoggingSanitizer.S(createFilter.IpAddressOrCidr ?? "")));
            return (false, "An unexpected error occurred", null);
        }
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string? ErrorMessage)> UpdateFilterAsync(UpdateIpFilterDto updateFilter)
    {
        try
        {
            _logger.LogDebug("Updating IP filter with ID: {FilterId}", updateFilter.Id);

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
            if (!changedProperties.Any())
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
            _logger.LogDebug("Deleting IP filter with ID: {FilterId}", id);

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
    public async Task<IpFilterSettingsDto> GetIpFilterSettingsAsync()
    {
        try
        {
            _logger.LogDebug("Getting IP filter settings");

            // Try to get settings from database first
            var enabledSetting = await _globalSettingRepository.GetByKeyAsync(SettingKeyEnabled);
            var defaultAllowSetting = await _globalSettingRepository.GetByKeyAsync(SettingKeyDefaultAllow);
            var bypassAdminUiSetting = await _globalSettingRepository.GetByKeyAsync(SettingKeyBypassForAdminUi);
            var excludedEndpointsSetting = await _globalSettingRepository.GetByKeyAsync(SettingKeyExcludedEndpoints);

            // Fall back to config file options if database settings don't exist
            var options = _ipFilterOptions.CurrentValue;

            var settings = new IpFilterSettingsDto
            {
                IsEnabled = enabledSetting != null
                    ? bool.TryParse(enabledSetting.Value, out var enabled) && enabled
                    : options.Enabled,

                DefaultAllow = defaultAllowSetting != null
                    ? bool.TryParse(defaultAllowSetting.Value, out var defaultAllow) && defaultAllow
                    : options.DefaultAllow,

                BypassForAdminUi = bypassAdminUiSetting != null
                    ? bool.TryParse(bypassAdminUiSetting.Value, out var bypass) && bypass
                    : options.BypassForAdminUi,

                ExcludedEndpoints = excludedEndpointsSetting != null
                    ? DeserializeExcludedEndpoints(excludedEndpointsSetting.Value)
                    : options.ExcludedEndpoints.ToList()
            };

            return settings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting IP filter settings");

            // Return default settings on error
            return new IpFilterSettingsDto
            {
                IsEnabled = false,
                DefaultAllow = true,
                BypassForAdminUi = true,
                ExcludedEndpoints = new List<string> { "/api/v1/health" }
            };
        }
    }

    /// <summary>
    /// Deserializes the excluded endpoints JSON string to a list
    /// </summary>
    private List<string> DeserializeExcludedEndpoints(string json)
    {
        try
        {
            var endpoints = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
            return endpoints ?? new List<string> { "/api/v1/health" };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize excluded endpoints JSON, using defaults");
            return new List<string> { "/api/v1/health" };
        }
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string? ErrorMessage)> UpdateIpFilterSettingsAsync(IpFilterSettingsDto settings)
    {
        try
        {
            _logger.LogDebug("Updating IP filter settings: Enabled={Enabled}, DefaultAllow={DefaultAllow}",
                settings.IsEnabled, settings.DefaultAllow);

            // Validate settings
            if (settings.ExcludedEndpoints == null)
            {
                settings.ExcludedEndpoints = new List<string>();
            }

            // Persist settings to database using GlobalSettingRepository
            await _globalSettingRepository.UpsertAsync(
                SettingKeyEnabled,
                settings.IsEnabled.ToString(),
                "Whether IP filtering is enabled");

            await _globalSettingRepository.UpsertAsync(
                SettingKeyDefaultAllow,
                settings.DefaultAllow.ToString(),
                "Default filter mode when no specific rules match (true = allow, false = deny)");

            await _globalSettingRepository.UpsertAsync(
                SettingKeyBypassForAdminUi,
                settings.BypassForAdminUi.ToString(),
                "Whether to bypass filtering for admin UI access");

            await _globalSettingRepository.UpsertAsync(
                SettingKeyExcludedEndpoints,
                System.Text.Json.JsonSerializer.Serialize(settings.ExcludedEndpoints),
                "List of endpoints to exclude from IP filtering");

            _logger.LogInformation("IP filter settings updated successfully in database");

            // Publish event for cache invalidation across services
            await PublishEventAsync(
                new IpFilterChanged
                {
                    FilterId = 0, // Settings change, not a specific filter
                    IpAddressOrCidr = "*",
                    FilterType = "settings",
                    IsEnabled = settings.IsEnabled,
                    ChangeType = "SettingsUpdated",
                    ChangedProperties = new[] { "IsEnabled", "DefaultAllow", "BypassForAdminUi", "ExcludedEndpoints" },
                    Description = "IP filter settings updated",
                    CorrelationId = Guid.NewGuid().ToString()
                },
                "update IP filter settings",
                new { IsEnabled = settings.IsEnabled, DefaultAllow = settings.DefaultAllow });

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating IP filter settings");
            return (false, "An unexpected error occurred while saving settings");
        }
    }

    /// <inheritdoc/>
    public async Task<IpCheckResult> CheckIpAddressAsync(string ipAddress)
    {
        try
        {
            _logger.LogDebug("Checking if IP address is allowed: {IpAddress}", LoggingSanitizer.S(ipAddress));

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
            var filtersList = filters.ToList();

            var hasWhitelist = filtersList.Any(f => f.FilterType == IpFilterConstants.WHITELIST);
            var hasBlacklist = filtersList.Any(f => f.FilterType == IpFilterConstants.BLACKLIST);

            // Check blacklist FIRST - if IP is blacklisted, deny immediately
            // This is the correct order: blacklist takes precedence
            if (hasBlacklist)
            {
                foreach (var filter in filtersList.Where(f => f.FilterType == IpFilterConstants.BLACKLIST))
                {
                    if (IpAddressHelper.IsIpInRange(ipAddress, filter.IpAddressOrCidr))
                    {
                        _logger.LogWarning("IP {IpAddress} is blacklisted by rule {Rule}",
                            LoggingSanitizer.S(ipAddress), LoggingSanitizer.S(filter.IpAddressOrCidr));
                        return new IpCheckResult
                        {
                            IsAllowed = false,
                            DeniedReason = $"IP address matched deny filter: {filter.Description ?? filter.IpAddressOrCidr}"
                        };
                    }
                }
            }

            // Check whitelist - if there's a whitelist, IP must be in it
            if (hasWhitelist)
            {
                foreach (var filter in filtersList.Where(f => f.FilterType == IpFilterConstants.WHITELIST))
                {
                    if (IpAddressHelper.IsIpInRange(ipAddress, filter.IpAddressOrCidr))
                    {
                        _logger.LogDebug("IP {IpAddress} is whitelisted by rule {Rule}",
                            LoggingSanitizer.S(ipAddress), LoggingSanitizer.S(filter.IpAddressOrCidr));
                        return new IpCheckResult { IsAllowed = true };
                    }
                }

                // Has whitelist but IP not in it
                _logger.LogWarning("IP {IpAddress} is not in whitelist", LoggingSanitizer.S(ipAddress));
                return new IpCheckResult
                {
                    IsAllowed = false,
                    DeniedReason = "IP address did not match any allow filters"
                };
            }

            // No whitelist and not blacklisted - use default policy
            return new IpCheckResult
            {
                IsAllowed = settings.DefaultAllow,
                DeniedReason = settings.DefaultAllow ? null : "IP address did not match any allow filters (default deny)"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if IP address is allowed: {IpAddress}", LoggingSanitizer.S(ipAddress));

            // On error, default to allowing the request (safer than potentially blocking all traffic)
            return new IpCheckResult
            {
                IsAllowed = true,
                DeniedReason = "Error during IP check, allowed as a failsafe"
            };
        }
    }

    /// <summary>
    /// Validates if a string is a valid IP address or CIDR notation.
    /// Delegates to IpAddressHelper for consistent validation.
    /// </summary>
    /// <param name="ipAddressOrCidr">The string to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    private bool IsValidIpAddressOrCidr(string ipAddressOrCidr)
    {
        return IpAddressHelper.IsValidIpAddressOrCidr(ipAddressOrCidr);
    }

    /// <inheritdoc/>
    public async Task<bool> IsIpAllowedAsync(string ipAddress)
    {
        var result = await CheckIpAddressAsync(ipAddress);
        return result.IsAllowed;
    }
}
