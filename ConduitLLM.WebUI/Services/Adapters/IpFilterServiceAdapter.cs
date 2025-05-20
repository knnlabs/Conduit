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
                var filters = await _adminApiClient.GetAllIpFiltersAsync();
                return filters.Where(f => f.IsEnabled);
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
                // Get the filter settings
                var settings = await GetIpFilterSettingsAsync();
                
                // If we're in permissive mode (no whitelist), the IP is allowed unless it's in the blacklist
                if (settings.FilterMode == "permissive")
                {
                    return !IsIpInFilterList(ipAddress, settings.BlacklistFilters);
                }
                
                // If we're in restrictive mode (has whitelist), the IP must be in the whitelist and not in the blacklist
                return IsIpInFilterList(ipAddress, settings.WhitelistFilters) && 
                       !IsIpInFilterList(ipAddress, settings.BlacklistFilters);
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
                var filters = await _adminApiClient.GetAllIpFiltersAsync();
                
                return new IpFilterSettings
                {
                    WhitelistFilters = filters
                        .Where(f => f.FilterType == "whitelist" && f.IsEnabled)
                        .OrderBy(f => f.Id)
                        .ToList(),
                    
                    BlacklistFilters = filters
                        .Where(f => f.FilterType == "blacklist" && f.IsEnabled)
                        .OrderBy(f => f.Id)
                        .ToList(),
                    
                    // Default mode if no whitelist filters exist is "permissive"
                    // This means anyone can access unless blacklisted
                    FilterMode = filters.Any(f => f.FilterType == "whitelist" && f.IsEnabled) 
                        ? "restrictive" 
                        : "permissive"
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
                    FilterMode = "permissive"
                };
            }
        }

        /// <inheritdoc />
        public async Task<(bool Success, string? ErrorMessage)> UpdateIpFilterSettingsAsync(IpFilterSettings settings)
        {
            try
            {
                // Get current filters
                var currentFilters = await _adminApiClient.GetAllIpFiltersAsync();
                
                // Get new filter mode
                var newMode = settings.FilterMode;
                if (newMode != "permissive" && newMode != "restrictive")
                {
                    return (false, "Filter mode must be 'permissive' or 'restrictive'");
                }

                // If switching to permissive mode, disable all whitelist filters
                if (newMode == "permissive")
                {
                    foreach (var filter in currentFilters.Where(f => f.FilterType == "whitelist"))
                    {
                        var updateDto = new UpdateIpFilterDto
                        {
                            Id = filter.Id,
                            IpAddress = filter.IpAddress,
                            Description = filter.Description,
                            FilterType = filter.FilterType,
                            IsEnabled = false // Disable the filter
                        };
                        
                        await _adminApiClient.UpdateIpFilterAsync(filter.Id, updateDto);
                    }
                }
                
                // If switching to restrictive mode, ensure we have at least one whitelist filter
                if (newMode == "restrictive" && !currentFilters.Any(f => f.FilterType == "whitelist" && f.IsEnabled))
                {
                    // Check if we have any disabled whitelist filters
                    var disabledWhitelists = currentFilters.Where(f => f.FilterType == "whitelist" && !f.IsEnabled).ToList();
                    
                    if (disabledWhitelists.Any())
                    {
                        // Enable the first one
                        var filter = disabledWhitelists.First();
                        var updateDto = new UpdateIpFilterDto
                        {
                            Id = filter.Id,
                            IpAddress = filter.IpAddress,
                            Description = filter.Description,
                            FilterType = filter.FilterType,
                            IsEnabled = true // Enable the filter
                        };
                        
                        await _adminApiClient.UpdateIpFilterAsync(filter.Id, updateDto);
                    }
                    else
                    {
                        // Create a default whitelist entry for localhost
                        var createDto = new CreateIpFilterDto
                        {
                            IpAddress = "127.0.0.1",
                            Description = "Default localhost whitelist entry",
                            FilterType = "whitelist",
                            IsEnabled = true
                        };
                        
                        await _adminApiClient.CreateIpFilterAsync(createDto);
                    }
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

        private bool IsIpInFilterList(string ipAddress, IEnumerable<IpFilterDto> filters)
        {
            if (!filters.Any())
                return false;

            // Parse the input IP address
            if (!IPAddress.TryParse(ipAddress, out var inputIp))
                return false;

            // Convert input IP to bytes for comparison
            var inputBytes = inputIp.GetAddressBytes();

            foreach (var filter in filters)
            {
                // Check if the filter is a CIDR range
                if (filter.IpAddress.Contains("/"))
                {
                    var cidrParts = filter.IpAddress.Split('/');
                    if (cidrParts.Length != 2 || !int.TryParse(cidrParts[1], out var prefixLength))
                        continue;

                    // Parse the network part of the CIDR
                    if (!IPAddress.TryParse(cidrParts[0], out var networkIp))
                        continue;

                    // Convert network IP to bytes
                    var networkBytes = networkIp.GetAddressBytes();

                    // Create network mask from prefix length
                    var mask = CreateMask(prefixLength, networkBytes.Length);

                    // Apply mask to both IPs and compare
                    bool match = true;
                    for (int i = 0; i < inputBytes.Length; i++)
                    {
                        if ((inputBytes[i] & mask[i]) != (networkBytes[i] & mask[i]))
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match)
                        return true;
                }
                else
                {
                    // Simple IP equality check
                    if (IPAddress.TryParse(filter.IpAddress, out var filterIp))
                    {
                        if (inputIp.Equals(filterIp))
                            return true;
                    }
                }
            }

            return false;
        }

        private byte[] CreateMask(int prefixLength, int byteLength)
        {
            var mask = new byte[byteLength];
            
            // Set all bits in mask according to prefix length
            for (int i = 0; i < byteLength; i++)
            {
                if (prefixLength >= 8)
                {
                    mask[i] = 255;
                    prefixLength -= 8;
                }
                else if (prefixLength > 0)
                {
                    mask[i] = (byte)(255 << (8 - prefixLength));
                    prefixLength = 0;
                }
                else
                {
                    mask[i] = 0;
                }
            }
            
            return mask;
        }
    }
}