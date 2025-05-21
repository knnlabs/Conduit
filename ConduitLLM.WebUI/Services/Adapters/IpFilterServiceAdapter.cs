using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs.IpFilter;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Models;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services.Adapters
{
    /// <summary>
    /// Adapter that implements <see cref="IIpFilterService"/> using the Admin API client.
    /// </summary>
    public class IpFilterServiceAdapter : IIpFilterService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<IpFilterServiceAdapter> _logger;
        private readonly Regex _ipRegex = new Regex(@"^(([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])(/([0-9]|[1-2][0-9]|3[0-2]))?$", RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of the <see cref="IpFilterServiceAdapter"/> class.
        /// </summary>
        /// <param name="adminApiClient">The Admin API client.</param>
        /// <param name="logger">The logger.</param>
        public IpFilterServiceAdapter(
            IAdminApiClient adminApiClient,
            ILogger<IpFilterServiceAdapter> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<IEnumerable<IpFilterDto>> GetAllFiltersAsync()
        {
            try
            {
                return await _adminApiClient.GetAllIpFiltersAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all IP filters");
                return Enumerable.Empty<IpFilterDto>();
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<IpFilterDto>> GetEnabledFiltersAsync()
        {
            try
            {
                return await _adminApiClient.GetEnabledIpFiltersAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting enabled IP filters");
                return Enumerable.Empty<IpFilterDto>();
            }
        }

        /// <inheritdoc />
        public async Task<IpFilterDto?> GetFilterByIdAsync(int id)
        {
            try
            {
                return await _adminApiClient.GetIpFilterByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting IP filter with ID {Id}", id);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<(bool Success, string? ErrorMessage, IpFilterDto? Filter)> CreateFilterAsync(CreateIpFilterDto filter)
        {
            try
            {
                // Validate the IP address/range
                if (!IsValidIpAddress(filter.IpAddress))
                {
                    return (false, "Invalid IP address or CIDR notation", null);
                }

                // Validate filter type
                if (filter.FilterType != "whitelist" && filter.FilterType != "blacklist")
                {
                    return (false, "Filter type must be 'whitelist' or 'blacklist'", null);
                }

                var result = await _adminApiClient.CreateIpFilterAsync(filter);
                if (result == null)
                {
                    return (false, "Failed to create IP filter", null);
                }

                return (true, null, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating IP filter for {IpAddress}", filter.IpAddress);
                return (false, $"Error creating IP filter: {ex.Message}", null);
            }
        }

        /// <inheritdoc />
        public async Task<(bool Success, string? ErrorMessage)> UpdateFilterAsync(UpdateIpFilterDto filter)
        {
            try
            {
                // Get the filter ID
                if (filter.Id <= 0)
                {
                    return (false, "Invalid filter ID");
                }

                // Validate the IP address/range if provided
                if (!string.IsNullOrEmpty(filter.IpAddress) && !IsValidIpAddress(filter.IpAddress))
                {
                    return (false, "Invalid IP address or CIDR notation");
                }

                // Validate filter type if provided
                if (!string.IsNullOrEmpty(filter.FilterType) && 
                    filter.FilterType != "whitelist" && filter.FilterType != "blacklist")
                {
                    return (false, "Filter type must be 'whitelist' or 'blacklist'");
                }

                var result = await _adminApiClient.UpdateIpFilterAsync(filter.Id, filter);
                if (result == null)
                {
                    return (false, "Failed to update IP filter");
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating IP filter with ID {Id}", filter.Id);
                return (false, $"Error updating IP filter: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public async Task<(bool Success, string? ErrorMessage)> DeleteFilterAsync(int id)
        {
            try
            {
                var success = await _adminApiClient.DeleteIpFilterAsync(id);
                if (!success)
                {
                    return (false, "Failed to delete IP filter");
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting IP filter with ID {Id}", id);
                return (false, $"Error deleting IP filter: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public async Task<bool> IsIpAllowedAsync(string ipAddress)
        {
            try
            {
                // Use the high-performance API endpoint instead of local processing
                var result = await _adminApiClient.CheckIpAddressAsync(ipAddress);
                
                // If the check fails (result is null), default to allowing the IP
                return result?.IsAllowed ?? true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if IP {IpAddress} is allowed", ipAddress);
                // In case of error, default to allowing the request
                return true;
            }
        }

        /// <inheritdoc />
        public async Task<IpFilterSettings> GetIpFilterSettingsAsync()
        {
            try
            {
                var settings = await _adminApiClient.GetIpFilterSettingsAsync();
                
                return new IpFilterSettings
                {
                    WhitelistFilters = settings.WhitelistFilters.ToList(),
                    BlacklistFilters = settings.BlacklistFilters.ToList(),
                    FilterMode = settings.FilterMode,
                    IsEnabled = settings.IsEnabled,
                    DefaultAllow = settings.DefaultAllow,
                    BypassForAdminUi = settings.BypassForAdminUi,
                    ExcludedEndpoints = settings.ExcludedEndpoints.ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting IP filter settings");
                
                // Return default settings
                return new IpFilterSettings
                {
                    WhitelistFilters = new List<IpFilterDto>(),
                    BlacklistFilters = new List<IpFilterDto>(),
                    FilterMode = "permissive",
                    IsEnabled = false,
                    DefaultAllow = true,
                    BypassForAdminUi = true,
                    ExcludedEndpoints = new List<string> { "/api/v1/health" }
                };
            }
        }

        /// <inheritdoc />
        public async Task<(bool Success, string? ErrorMessage)> UpdateIpFilterSettingsAsync(IpFilterSettings settings)
        {
            try
            {
                var settingsDto = new IpFilterSettingsDto
                {
                    IsEnabled = settings.IsEnabled,
                    DefaultAllow = settings.DefaultAllow,
                    BypassForAdminUi = settings.BypassForAdminUi,
                    FilterMode = settings.FilterMode,
                    ExcludedEndpoints = settings.ExcludedEndpoints,
                    WhitelistFilters = settings.WhitelistFilters.ToList(),
                    BlacklistFilters = settings.BlacklistFilters.ToList()
                };
                
                var success = await _adminApiClient.UpdateIpFilterSettingsAsync(settingsDto);
                
                if (!success)
                {
                    return (false, "Failed to update IP filter settings");
                }
                
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating IP filter settings");
                return (false, $"Error updating IP filter settings: {ex.Message}");
            }
        }

        // Helper methods

        private bool IsValidIpAddress(string ipAddress)
        {
            return _ipRegex.IsMatch(ipAddress);
        }
    }
}