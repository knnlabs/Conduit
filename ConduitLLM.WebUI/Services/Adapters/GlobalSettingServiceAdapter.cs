using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs;
using ConduitLLM.WebUI.Interfaces;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services.Adapters
{
    /// <summary>
    /// Adapter that bridges the global settings service interface with the Admin API client
    /// </summary>
    public class GlobalSettingServiceAdapter
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<GlobalSettingServiceAdapter> _logger;

        public GlobalSettingServiceAdapter(IAdminApiClient adminApiClient, ILogger<GlobalSettingServiceAdapter> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets all global settings
        /// </summary>
        /// <returns>Collection of global settings</returns>
        public async Task<IEnumerable<GlobalSettingDto>> GetAllGlobalSettingsAsync()
        {
            return await _adminApiClient.GetAllGlobalSettingsAsync();
        }

        /// <summary>
        /// Gets a global setting by key
        /// </summary>
        /// <param name="key">The setting key</param>
        /// <returns>The global setting or null if not found</returns>
        public async Task<GlobalSettingDto?> GetGlobalSettingByKeyAsync(string key)
        {
            return await _adminApiClient.GetGlobalSettingByKeyAsync(key);
        }

        /// <summary>
        /// Creates or updates a global setting
        /// </summary>
        /// <param name="setting">The setting to create or update</param>
        /// <returns>The created or updated setting</returns>
        public async Task<GlobalSettingDto?> UpsertGlobalSettingAsync(GlobalSettingDto setting)
        {
            return await _adminApiClient.UpsertGlobalSettingAsync(setting);
        }

        /// <summary>
        /// Deletes a global setting
        /// </summary>
        /// <param name="key">The key of the setting to delete</param>
        /// <returns>True if deleted successfully</returns>
        public async Task<bool> DeleteGlobalSettingAsync(string key)
        {
            return await _adminApiClient.DeleteGlobalSettingAsync(key);
        }

        /// <summary>
        /// Gets the value of a global setting
        /// </summary>
        /// <param name="key">The setting key</param>
        /// <returns>The setting value or null if not found</returns>
        public async Task<string?> GetSettingValueAsync(string key)
        {
            var setting = await _adminApiClient.GetGlobalSettingByKeyAsync(key);
            return setting?.Value;
        }

        /// <summary>
        /// Sets the value of a global setting
        /// </summary>
        /// <param name="key">The setting key</param>
        /// <param name="value">The setting value</param>
        /// <returns>True if set successfully</returns>
        public async Task<bool> SetSettingValueAsync(string key, string value)
        {
            // First check if the setting exists to preserve its ID
            var existingSetting = await _adminApiClient.GetGlobalSettingByKeyAsync(key);
            
            var setting = new GlobalSettingDto
            {
                Key = key,
                Value = value,
                Id = existingSetting?.Id ?? 0  // Preserve existing ID if updating
            };

            var result = await _adminApiClient.UpsertGlobalSettingAsync(setting);
            return result != null;
        }
    }
}