using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs;
using ConduitLLM.WebUI.Interfaces;

namespace ConduitLLM.WebUI.Extensions
{
    /// <summary>
    /// Extension methods for IAdminApiClient to handle GlobalSettings operations
    /// </summary>
    public static class GlobalSettingServiceExtensions
    {
        /// <summary>
        /// Gets a global setting by key (backward compatibility method)
        /// </summary>
        /// <param name="client">The admin API client</param>
        /// <param name="key">The setting key</param>
        /// <returns>The global setting DTO, or null if not found</returns>
        public static async Task<GlobalSettingDto?> GetGlobalSettingAsync(
            this IAdminApiClient client,
            string key)
        {
            return await client.GetGlobalSettingByKeyAsync(key);
        }

        /// <summary>
        /// Creates or updates a global setting (backward compatibility method)
        /// </summary>
        /// <param name="client">The admin API client</param>
        /// <param name="setting">The setting to create or update</param>
        /// <returns>The created or updated setting</returns>
        public static async Task<GlobalSettingDto?> CreateOrUpdateGlobalSettingAsync(
            this IAdminApiClient client,
            GlobalSettingDto setting)
        {
            return await client.UpsertGlobalSettingAsync(setting);
        }

        /// <summary>
        /// Gets a string value for a global setting by key
        /// </summary>
        /// <param name="client">The admin API client</param>
        /// <param name="key">The setting key</param>
        /// <returns>The setting value, or null if not found</returns>
        public static async Task<string?> GetGlobalSettingValueAsync(
            this IAdminApiClient client,
            string key)
        {
            var setting = await client.GetGlobalSettingByKeyAsync(key);
            return setting?.Value;
        }

        /// <summary>
        /// Sets a global setting with the specified key and value
        /// </summary>
        /// <param name="client">The admin API client</param>
        /// <param name="key">The setting key</param>
        /// <param name="value">The setting value</param>
        /// <param name="description">Optional description of the setting</param>
        /// <returns>The updated setting</returns>
        public static async Task<GlobalSettingDto?> SetGlobalSettingAsync(
            this IAdminApiClient client,
            string key,
            string value,
            string? description = null)
        {
            var setting = new GlobalSettingDto
            {
                Key = key,
                Value = value,
                Description = description ?? $"Setting for {key}"
            };

            return await client.UpsertGlobalSettingAsync(setting);
        }
    }
}
