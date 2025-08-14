using System.Threading.Tasks;

namespace ConduitLLM.Http.Interfaces
{
    /// <summary>
    /// Service for refreshing in-memory settings from the database
    /// </summary>
    public interface ISettingsRefreshService
    {
        /// <summary>
        /// Refreshes model mappings from the database
        /// </summary>
        Task RefreshModelMappingsAsync();

        /// <summary>
        /// Refreshes provider credentials from the database
        /// </summary>
        Task RefreshProvidersAsync();

        /// <summary>
        /// Refreshes all settings from the database
        /// </summary>
        Task RefreshAllSettingsAsync();
    }
}