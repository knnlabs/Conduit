using ConduitLLM.Configuration.DTOs;

namespace ConduitLLM.Admin.Interfaces
{
    /// <summary>
    /// Service interface for managing global settings through the Admin API
    /// </summary>
    public interface IAdminGlobalSettingService
    {
        /// <summary>
        /// Gets all global settings
        /// </summary>
        /// <returns>A list of all global settings</returns>
        Task<IEnumerable<GlobalSettingDto>> GetAllSettingsAsync();

        /// <summary>
        /// Gets a global setting by ID
        /// </summary>
        /// <param name="id">The setting ID</param>
        /// <returns>The global setting, or null if not found</returns>
        Task<GlobalSettingDto?> GetSettingByIdAsync(int id);

        /// <summary>
        /// Gets a global setting by key
        /// </summary>
        /// <param name="key">The setting key</param>
        /// <returns>The global setting, or null if not found</returns>
        Task<GlobalSettingDto?> GetSettingByKeyAsync(string key);

        /// <summary>
        /// Creates a new global setting
        /// </summary>
        /// <param name="setting">The setting to create</param>
        /// <returns>The created setting</returns>
        Task<GlobalSettingDto> CreateSettingAsync(CreateGlobalSettingDto setting);

        /// <summary>
        /// Updates a global setting
        /// </summary>
        /// <param name="setting">The setting to update</param>
        /// <returns>True if the update was successful, false otherwise</returns>
        Task<bool> UpdateSettingAsync(UpdateGlobalSettingDto setting);

        /// <summary>
        /// Updates a global setting by key
        /// </summary>
        /// <param name="setting">The setting to update</param>
        /// <returns>True if the update was successful, false otherwise</returns>
        Task<bool> UpdateSettingByKeyAsync(UpdateGlobalSettingByKeyDto setting);

        /// <summary>
        /// Deletes a global setting
        /// </summary>
        /// <param name="id">The ID of the setting to delete</param>
        /// <returns>True if the deletion was successful, false otherwise</returns>
        Task<bool> DeleteSettingAsync(int id);

        /// <summary>
        /// Deletes a global setting by key
        /// </summary>
        /// <param name="key">The key of the setting to delete</param>
        /// <returns>True if the deletion was successful, false otherwise</returns>
        Task<bool> DeleteSettingByKeyAsync(string key);
    }
}
