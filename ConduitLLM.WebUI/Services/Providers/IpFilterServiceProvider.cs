using ConduitLLM.Configuration.DTOs.IpFilter;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Services.Providers
{
    /// <summary>
    /// Implementation of IIpFilterService that uses IAdminApiClient to interact with the Admin API.
    /// </summary>
    public class IpFilterServiceProvider : IIpFilterService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<IpFilterServiceProvider> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="IpFilterServiceProvider"/> class.
        /// </summary>
        /// <param name="adminApiClient">The Admin API client.</param>
        /// <param name="logger">The logger.</param>
        public IpFilterServiceProvider(
            IAdminApiClient adminApiClient,
            ILogger<IpFilterServiceProvider> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<(bool Success, string? ErrorMessage, IpFilterDto? Filter)> CreateFilterAsync(CreateIpFilterDto filter)
        {
            try
            {
                var newFilter = await _adminApiClient.CreateIpFilterAsync(filter);

                if (newFilter != null)
                {
                    return (true, null, newFilter);
                }
                else
                {
                    return (false, "Failed to create IP filter", null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating IP filter for {IpAddress}", filter.IpAddressOrCidr);
                return (false, $"Error creating IP filter: {ex.Message}", null);
            }
        }

        /// <inheritdoc />
        public async Task<(bool Success, string? ErrorMessage)> DeleteFilterAsync(int id)
        {
            try
            {
                var success = await _adminApiClient.DeleteIpFilterAsync(id);
                
                if (success)
                {
                    return (true, null);
                }
                else
                {
                    return (false, "Failed to delete IP filter");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting IP filter with ID {IpFilterId}", id);
                return (false, $"Error deleting IP filter: {ex.Message}");
            }
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
                _logger.LogError(ex, "Error retrieving all IP filters");
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
                _logger.LogError(ex, "Error retrieving enabled IP filters");
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
                _logger.LogError(ex, "Error retrieving IP filter with ID {IpFilterId}", id);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<ConduitLLM.Configuration.DTOs.IpFilter.IpFilterSettingsDto> GetIpFilterSettingsAsync()
        {
            try
            {
                return await _adminApiClient.GetIpFilterSettingsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving IP filter settings");
                
                // Return default settings
                return new ConduitLLM.Configuration.DTOs.IpFilter.IpFilterSettingsDto
                {
                    IsEnabled = false,
                    DefaultAllow = true,
                    BypassForAdminUi = true,
                    FilterMode = "permissive",
                    ExcludedEndpoints = new List<string> { "/api/v1/health" },
                    WhitelistFilters = new List<ConduitLLM.Configuration.DTOs.IpFilter.IpFilterDto>(),
                    BlacklistFilters = new List<ConduitLLM.Configuration.DTOs.IpFilter.IpFilterDto>()
                };
            }
        }

        /// <inheritdoc />
        public async Task<bool> IsIpAllowedAsync(string ipAddress)
        {
            try
            {
                var result = await _adminApiClient.CheckIpAddressAsync(ipAddress);
                return result?.IsAllowed ?? true; // Default to allowed if the check fails
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if IP address {IpAddress} is allowed", ipAddress);
                return true; // Default to allowed if the check fails
            }
        }

        /// <inheritdoc />
        public async Task<(bool Success, string? ErrorMessage)> UpdateFilterAsync(UpdateIpFilterDto filter)
        {
            try
            {
                var updatedFilter = await _adminApiClient.UpdateIpFilterAsync(filter.Id, filter);
                
                if (updatedFilter != null)
                {
                    return (true, null);
                }
                else
                {
                    return (false, "Failed to update IP filter");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating IP filter with ID {IpFilterId}", filter.Id);
                return (false, $"Error updating IP filter: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public async Task<(bool Success, string? ErrorMessage)> UpdateIpFilterSettingsAsync(ConduitLLM.Configuration.DTOs.IpFilter.IpFilterSettingsDto settings)
        {
            try
            {
                var success = await _adminApiClient.UpdateIpFilterSettingsAsync(settings);
                
                if (success)
                {
                    return (true, null);
                }
                else
                {
                    return (false, "Failed to update IP filter settings");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating IP filter settings");
                return (false, $"Error updating IP filter settings: {ex.Message}");
            }
        }
    }
}