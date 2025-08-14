using System.Threading.Tasks;

namespace ConduitLLM.Configuration.Interfaces
{
    /// <summary>
    /// Service for accessing global application settings
    /// </summary>
    public interface IGlobalSettingService
    {
        /// <summary>
        /// Gets a setting value by key
        /// </summary>
        /// <param name="key">The setting key</param>
        /// <returns>The setting value, or null if not found</returns>
        Task<string?> GetSettingAsync(string key);

        /// <summary>
        /// Sets a setting value
        /// </summary>
        /// <param name="key">The setting key</param>
        /// <param name="value">The setting value</param>
        /// <param name="description">Optional description of the setting</param>
        Task SetSettingAsync(string key, string value, string? description = null);
    }
}
