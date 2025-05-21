using ConduitLLM.Configuration.DTOs;
using ConduitLLM.WebUI.Interfaces;
using System.Threading.Tasks;

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
    }
}